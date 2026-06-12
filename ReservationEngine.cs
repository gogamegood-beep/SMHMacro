using System.Text.Json;

namespace SMH_android;

/// <summary>
/// WebView 를 직접 조종해 예약을 자동화한다(서버리스 · 온디바이스).
///  - 페이지를 WebView 에 로드 → JS 주입으로 슬롯 탐색/폼 작성/제출
///  - 모든 호출은 UI 스레드에서 수행해야 함.
/// </summary>
public class ReservationEngine : IDisposable
{
    private WebView? _web;
    private readonly Action<string> _log;
    private TaskCompletionSource<bool>? _navTcs;
    private bool _disposed;

    public ReservationEngine(WebView web, Action<string> log)
    {
        _web = web;
        _log = log;
        _web.Navigated += OnNavigated;
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
        => _navTcs?.TrySetResult(e.Result == WebNavigationResult.Success);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_web != null) _web.Navigated -= OnNavigated;
        _navTcs?.TrySetResult(false);
        _web = null;
        GC.SuppressFinalize(this);
    }

    private WebView Web => _web ?? throw new ObjectDisposedException(nameof(ReservationEngine));

    private async Task<bool> NavigateAsync(string url, int timeoutMs = 15000)
    {
        _navTcs = new TaskCompletionSource<bool>();
        // 캐시 무력화로 매번 강제 새로고침
        var bust = (url.Contains('?') ? "&" : "?") + "_=" + DateTimeOffset.Now.ToUnixTimeMilliseconds();
        Web.Source = new UrlWebViewSource { Url = url + bust };
        var done = await Task.WhenAny(_navTcs.Task, Task.Delay(timeoutMs));
        return done == _navTcs.Task && _navTcs.Task.Result;
    }

    private async Task<string> EvalAsync(string js)
    {
        var r = await Web.EvaluateJavaScriptAsync(js);
        if (string.IsNullOrEmpty(r)) return "";
        r = r.Trim();
        if (r.Length >= 2 && r.StartsWith('"') && r.EndsWith('"'))
            r = r[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return r;
    }

    public async Task<ReservationResult> RunAsync(ResvSettings cfg, bool waitForOpen, CancellationToken ct)
    {
        var months = cfg.AutoScan
            ? new (int Y, int M)[] { (DateTime.Now.Year, DateTime.Now.Month), (DateTime.Now.AddMonths(1).Year, DateTime.Now.AddMonths(1).Month) }
            : new (int Y, int M)[] { (cfg.ScanYear, cfg.ScanMonth) };

        if (waitForOpen) await WaitUntilOpenAsync(cfg, ct);

        var deadline = DateTime.Now.AddMinutes(cfg.MaxPollMinutes);
        bool opened = false; string slot = ""; int attempt = 0;

        while (DateTime.Now < deadline && !ct.IsCancellationRequested)
        {
            attempt++;
            foreach (var ym in months)
            {
                ct.ThrowIfCancellationRequested();
                var url = $"{cfg.BaseUrl}&year={ym.Y}&month={ym.M}";
                if (!await NavigateAsync(url)) { _log("페이지 로드 실패, 재시도"); continue; }

                var type = JsonSerializer.Serialize(cfg.Type);
                // 데스크톱(em '초진' + 형제 a.com_btn)과 모바일(em.s_mobile_state '초진 예약하기' 한 덩어리) 둘 다 지원
                var found = await EvalAsync(
                    "(function(type){function open(s){return s.indexOf('예약')>=0&&s.indexOf('완료')<0;}" +
                    "var ems=Array.prototype.slice.call(document.querySelectorAll('em'));" +
                    "for(var i=0;i<ems.length;i++){var em=ems[i];var txt=(em.textContent||'').trim();" +
                    // 모바일: 타입+'예약'(완료 아님)이 한 요소 안에
                    "if(txt.indexOf(type)>=0&&open(txt)){var inner=em.querySelector('a');" +
                    "(inner||em).setAttribute('data-pick','1');return '1';}" +
                    // 데스크톱: em 텍스트가 정확히 type, 형제 a 가 '예약'(완료 아님)
                    "if(txt===type){var a=em.nextElementSibling;while(a&&a.tagName!=='A')a=a.nextElementSibling;" +
                    "if(a&&open(a.textContent||'')){a.setAttribute('data-pick','1');return '1';}}}" +
                    "return '0';})(" + type + ")");

                if (found == "1")
                {
                    opened = true;
                    slot = $"{ym.Y}-{ym.M:00} {cfg.Type}";
                    _log($"[{attempt}회차] 열린 슬롯 발견! ({slot}) → 진입");
                    break;
                }
            }
            if (opened) break;
            if (attempt % 10 == 0) _log($"…{attempt}회 폴링 중");
            // 새로고침 간격: 1~5초(설정) 사이 랜덤
            var wait = Random.Shared.Next(cfg.PollMinMs, cfg.PollMaxMs + 1);
            await Task.Delay(wait, ct);
        }

        if (!opened)
        {
            _log($"열린 '{cfg.Type}' 슬롯을 찾지 못함 — 종료");
            return new ReservationResult { Success = false, Reason = "no-slot" };
        }

        // 예약하기 클릭 → 폼 페이지
        _navTcs = new TaskCompletionSource<bool>();
        await EvalAsync("(function(){var a=document.querySelector('[data-pick=\\\"1\\\"]');if(a){a.click();return '1';}return '0';})()");
        await Task.WhenAny(_navTcs.Task, Task.Delay(15000));

        // 폼 로드 대기
        if (!await WaitSelectorAsync("#reservation_name", 10000))
        {
            _log("예약 폼을 찾지 못함(구조 상이 가능) — 중단");
            return new ReservationResult { Success = false, Reason = "no-form", Slot = slot };
        }
        _log("예약 폼 진입 — 입력 시작");

        // 폼 채우기 — 사람처럼 한 칸씩, 각 입력 사이 200~500ms(설정) 랜덤 지연
        async Task Gap() => await Task.Delay(Random.Shared.Next(cfg.InputMinMs, cfg.InputMaxMs + 1), ct);

        await SetFieldAsync("reservation_name", cfg.Applicant); await Gap();
        await SelectCarrierAsync(cfg.PhoneCarrier); await Gap();
        await SetFieldAsync("reservation_phone2", cfg.Phone2); await Gap();
        await SetFieldAsync("reservation_phone3", cfg.Phone3); await Gap();
        await SetFieldAsync("reservation_password", cfg.Password); await Gap();
        await SetFieldAsync("reservation_password_re", cfg.Password); await Gap();

        var args = JsonSerializer.Serialize(new { same = cfg.PatientSameAsApplicant, patient = cfg.PatientName });
        var checkInfo = await EvalAsync(CheckScript(args));
        _log("입력 완료 · 체크/환자명: " + checkInfo);

        if (cfg.DryRun)
        {
            _log("테스트(DryRun) — 신청하기 누르지 않음");
            return new ReservationResult { Success = true, DryRun = true, Slot = slot };
        }

        // 짧은 자연 지연 후 제출
        await Task.Delay(150 + Random.Shared.Next(250), ct); // 0.15~0.4초
        _navTcs = new TaskCompletionSource<bool>();
        var clicked = await EvalAsync(
            "(function(){var b=document.querySelector('input[type=submit][value*=\\\"신청\\\"]');" +
            "if(!b){var arr=Array.prototype.slice.call(document.querySelectorAll('button,a'));" +
            "b=arr.filter(function(e){return e.textContent.indexOf('신청하기')>=0;})[0];}" +
            "if(b){b.click();return '1';}return '0';})()");
        // 클릭 즉시 로그(정확한 시각). 이후의 시간은 서버 확인 페이지 응답 대기.
        _log(clicked == "1" ? "신청하기 클릭함 — 확인 메시지 대기" : "신청 버튼을 찾지 못함");
        if (clicked != "1")
            return new ReservationResult { Success = false, Reason = "no-submit", Slot = slot };

        // JS 확인창은 자동 확인됨. 확인 페이지/메시지 도착 대기.
        await Task.WhenAny(_navTcs.Task, Task.Delay(8000));
        await Task.Delay(600); // 결과 메시지 렌더 여유

        // 예약 완료 메시지 검출
        var body = await EvalAsync("(document.body?document.body.innerText:'').slice(0,3000)");
        bool done = body.Contains("완료") || body.Contains("접수") || body.Contains("신청되") || body.Contains("예약되");
        _log(done ? "✅ 예약 완료 메시지 확인됨" : "신청 후 결과 메시지를 못 찾음(스크린샷 확인 필요)");
        return new ReservationResult { Success = true, Slot = slot, Done = done };
    }

    private async Task<bool> WaitSelectorAsync(string sel, int timeoutMs)
    {
        var until = DateTime.Now.AddMilliseconds(timeoutMs);
        var q = JsonSerializer.Serialize(sel);
        while (DateTime.Now < until)
        {
            if (await EvalAsync($"(document.querySelector({q})?'1':'0')") == "1") return true;
            await Task.Delay(200);
        }
        return false;
    }

    private async Task WaitUntilOpenAsync(ResvSettings cfg, CancellationToken ct)
    {
        if (!TimeSpan.TryParse(cfg.OpenTime, out var open)) return;
        var target = DateTime.Now.Date + open - TimeSpan.FromSeconds(cfg.StartLeadSeconds);
        if (target <= DateTime.Now) { _log("오픈 시각 지남/임박 → 즉시 폴링"); return; }
        _log($"오픈 {cfg.StartLeadSeconds}초 전({target:HH:mm:ss})까지 대기");
        while (DateTime.Now < target)
        {
            ct.ThrowIfCancellationRequested();
            var remain = (target - DateTime.Now).TotalMilliseconds;
            await Task.Delay(remain > 30000 ? 5000 : 200, ct);
        }
    }

    // 텍스트 칸 1개 입력(focus + value + input/keyup 이벤트)
    private async Task SetFieldAsync(string id, string value)
    {
        var js =
            "(function(id,v){var e=document.getElementById(id);if(!e)return '0';" +
            "e.focus();e.value=v;e.dispatchEvent(new Event('input',{bubbles:true}));" +
            "e.dispatchEvent(new Event('keyup',{bubbles:true}));return '1';})(" +
            JsonSerializer.Serialize(id) + "," + JsonSerializer.Serialize(value) + ")";
        await EvalAsync(js);
    }

    // 통신사 select: 보이는 라벨 또는 value 로 선택
    private async Task SelectCarrierAsync(string carrier)
    {
        var js =
            "(function(label){var sel=document.getElementById('reservation_phone1');if(!sel)return '0';" +
            "for(var i=0;i<sel.options.length;i++){var o=sel.options[i];" +
            "if(o.textContent.trim()===label||o.value===label){sel.value=o.value;" +
            "sel.dispatchEvent(new Event('change',{bubbles:true}));return '1';}}return '0';})(" +
            JsonSerializer.Serialize(carrier) + ")";
        await EvalAsync(js);
    }

    // 동의 체크박스 + (필요 시)환자명. 정확한 name 미확정 → 라벨·주변 텍스트 기반
    private static string CheckScript(string argsJson) =>
        "(function(a){var out=[];var cbs=Array.prototype.slice.call(document.querySelectorAll('input[type=checkbox]'));" +
        "function textOf(el){var t='';if(el.id){var l=document.querySelector(\"label[for='\"+el.id+\"']\");if(l)t+=l.innerText;}" +
        "if(el.closest('label'))t+=el.closest('label').innerText;var p=el.closest('li,div,td,p,span');if(p)t+=' '+p.innerText;return t;}" +
        "cbs.forEach(function(cb){var t=textOf(cb);" +
        "if(t.indexOf('동일')>=0){if(a.same){if(!cb.checked)cb.click();out.push('동일');}else{if(cb.checked)cb.click();}}" +
        "else if(t.indexOf('동의')>=0||t.indexOf('확인하')>=0||t.indexOf('주의')>=0){if(!cb.checked)cb.click();out.push('동의');}});" +
        "if(!a.same&&a.patient){var inp=null;var labels=Array.prototype.slice.call(document.querySelectorAll('th,td,label,span,div'))" +
        ".filter(function(e){return e.innerText&&e.innerText.trim().indexOf('환자')===0;});" +
        "for(var j=0;j<labels.length;j++){var sc=labels[j].closest('tr,li,div')||document;" +
        "var ii=sc.querySelector('input[type=text]:not(#reservation_name)');if(ii){inp=ii;break;}}" +
        "if(inp){inp.value=a.patient;inp.dispatchEvent(new Event('input',{bubbles:true}));out.push('환자명');}}" +
        "return out.join(',');})(" + argsJson + ")";
}

public class ReservationResult
{
    public bool Success { get; set; }
    public bool DryRun { get; set; }
    public bool Done { get; set; }      // 예약 완료 메시지까지 확인됨
    public string? Reason { get; set; }
    public string Slot { get; set; } = "";
}
