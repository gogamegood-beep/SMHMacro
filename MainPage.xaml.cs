using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace SMH_android;

public partial class MainPage : ContentPage
{
    private ReservationEngine? _engine;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _log = new();

    public MainPage()
    {
        InitializeComponent();
        lblVersion.Text = $"버전 v{AppInfo.Current.VersionString}";
        chkSame.CheckedChanged += (_, _) => UpdatePatientEnabled();
        LoadToUi(ResvSettings.Load());
    }

    // "신청자와 동일" 체크 시 환자명 입력 비활성화
    private void UpdatePatientEnabled() => txtPatient.IsEnabled = !chkSame.IsChecked;

    // ── 설정 ↔ UI ──
    private void LoadToUi(ResvSettings s)
    {
        cboType.SelectedItem = s.Type;
        txtApplicant.Text = s.Applicant;
        chkSame.IsChecked = s.PatientSameAsApplicant;
        txtPatient.Text = s.PatientName;
        cboCarrier.SelectedItem = s.PhoneCarrier;
        txtPhone2.Text = s.Phone2;
        txtPhone3.Text = s.Phone3;
        txtPassword.Text = s.Password;
        tpOpenTime.Time = TimeSpan.TryParse(s.OpenTime, out var tod) ? tod : new TimeSpan(20, 0, 0);
        txtPollMin.Text = (s.PollMinMs / 1000.0).ToString("0.0"); // ms → 초
        txtPollMax.Text = (s.PollMaxMs / 1000.0).ToString("0.0");
        txtMax.Text = s.MaxPollMinutes.ToString();
        chkAutoScan.IsChecked = s.AutoScan;
        chkDryRun.IsChecked = s.DryRun;
        txtAlarmLead.Text = s.AlarmLeadMinutes.ToString();
        UpdatePatientEnabled();
    }

    private ResvSettings ReadFromUi()
    {
        // 초(0.0) 입력 → ms 로 변환
        double.TryParse(txtPollMin.Text, out var pollMinSec);
        double.TryParse(txtPollMax.Text, out var pollMaxSec);
        int.TryParse(txtMax.Text, out var max);
        int.TryParse(txtAlarmLead.Text, out var lead);
        var pollMin = (int)Math.Round(pollMinSec * 1000);
        var pollMax = (int)Math.Round(pollMaxSec * 1000);
        if (pollMin < 200) pollMin = 1000;
        if (pollMax < pollMin) pollMax = Math.Max(pollMin, 5000);
        var openTod = tpOpenTime.Time ?? new TimeSpan(20, 0, 0);
        return new ResvSettings
        {
            AlarmLeadMinutes = lead > 0 ? lead : 5,
            AlarmEnabled = Preferences.Get(nameof(ResvSettings.AlarmEnabled), false),
            PollMinMs = pollMin,
            PollMaxMs = pollMax,
            Type = cboType.SelectedItem as string ?? "초진",
            Applicant = txtApplicant.Text?.Trim() ?? "",
            PatientSameAsApplicant = chkSame.IsChecked,
            PatientName = txtPatient.Text?.Trim() ?? "",
            PhoneCarrier = cboCarrier.SelectedItem as string ?? "010",
            Phone2 = txtPhone2.Text?.Trim() ?? "",
            Phone3 = txtPhone3.Text?.Trim() ?? "",
            Password = txtPassword.Text?.Trim() ?? "",
            OpenTime = $"{openTod.Hours:00}:{openTod.Minutes:00}",
            MaxPollMinutes = max > 0 ? max : 6,
            AutoScan = chkAutoScan.IsChecked,
            DryRun = chkDryRun.IsChecked,
        };
    }

    private void OnSave(object? sender, EventArgs e)
    {
        ReadFromUi().Save();
        Log("설정 저장됨");
    }

    // 입력칸 바깥(빈 영역) 터치 → 키보드 내리기
    private void OnBackgroundTapped(object? sender, EventArgs e) => DismissKeyboard();

    private static void DismissKeyboard()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity == null) return;
        var imm = (Android.Views.InputMethods.InputMethodManager?)
            activity.GetSystemService(Android.Content.Context.InputMethodService);
        var token = activity.CurrentFocus?.WindowToken;
        imm?.HideSoftInputFromWindow(token, Android.Views.InputMethods.HideSoftInputFlags.None);
        activity.CurrentFocus?.ClearFocus();
#endif
    }

    private async void OnRunNow(object? sender, EventArgs e) => await Run(waitForOpen: false);
    private async void OnScheduleRun(object? sender, EventArgs e) => await Run(waitForOpen: true);

    private async Task Run(bool waitForOpen)
    {
        var cfg = ReadFromUi();
        cfg.Save();
        if (string.IsNullOrWhiteSpace(cfg.Applicant)) { await DisplayAlertAsync("확인", "신청자명을 입력하세요.", "확인"); return; }
        if (cfg.Phone2.Length == 0 || cfg.Phone3.Length == 0) { await DisplayAlertAsync("확인", "연락처를 입력하세요.", "확인"); return; }
        if (cfg.Password.Length == 0) { await DisplayAlertAsync("확인", "비밀번호를 입력하세요.", "확인"); return; }

        // 테스트(지금 실행)인데 예약 당일 오픈 10분 이내면 한 번 더 확인
        if (!waitForOpen && cfg.DryRun && IsNearOpeningOnReservationDay(cfg))
        {
            bool ok = await DisplayAlertAsync("확인",
                "금일은 예약 당일입니다. 테스트를 하시는 게 맞습니까?", "확인", "취소");
            if (!ok) return;
        }

        _log.Clear(); txtLog.Text = "";
        imgShot.Source = null;
        SetRunning(true);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _engine ??= new ReservationEngine(web, Log);

        try
        {
            var result = await _engine.RunAsync(cfg, waitForOpen, _cts.Token);
            if (!result.Success)
                Log($"미완료 · {result.Reason}");
            else if (result.DryRun)
                Log($"완료(테스트) · 슬롯={result.Slot}");
            else if (result.Verified)
                Log($"🎉 예약 완료 + 예약확인 조회 성공! · 슬롯={result.Slot} (아래 스크린샷 = 예약 내역)");
            else
                Log(result.Done
                    ? $"🎉 예약 완료! · 슬롯={result.Slot} (예약확인 조회는 미확인 — 스크린샷 참조)"
                    : $"신청함(완료 메시지 미확인) · 슬롯={result.Slot} — 스크린샷 확인");
            await CaptureAsync();
        }
        catch (OperationCanceledException) { Log("■ 사용자 중지"); }
        catch (Exception ex) { Log("‼ 오류: " + ex.Message); }
        finally { SetRunning(false); }
    }

    private void OnStop(object? sender, EventArgs e)
    {
        _cts?.Cancel();
        Log("중지 요청…");
    }

    // 오늘이 예약 오픈일(목/금/일/월)이고 오픈까지 10분 이내인가
    private static bool IsNearOpeningOnReservationDay(ResvSettings cfg)
    {
        var now = DateTime.Now;
        var days = new[] { DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Sunday, DayOfWeek.Monday };
        if (Array.IndexOf(days, now.DayOfWeek) < 0) return false;
        if (!TimeSpan.TryParse(cfg.OpenTime, out var open)) return false;
        var delta = (now.Date + open) - now;
        return delta > TimeSpan.Zero && delta <= TimeSpan.FromMinutes(10);
    }

    // ── 정시 알람 ──
    private IAlarmService? Alarm => IPlatformApplication.Current?.Services.GetService<IAlarmService>();

    private void OnScheduleAlarm(object? sender, EventArgs e)
    {
        var cfg = ReadFromUi();
        cfg.AlarmEnabled = true;
        cfg.Save();

        var svc = Alarm;
        if (svc == null) { lblAlarm.Text = "이 플랫폼에서는 알람을 지원하지 않습니다."; return; }
        svc.EnsureNotificationPermission();   // 알림 권한 확보(Android 13+)
        if (!svc.CanScheduleExact())
        {
            lblAlarm.Text = "정확 알람 권한이 필요합니다 → '정확 알람 확인'을 눌러 허용 후 다시 시도.";
            return;
        }
        svc.ScheduleNext(out var msg);
        lblAlarm.Text = msg;
        Log(msg);
    }

    private void OnCancelAlarm(object? sender, EventArgs e)
    {
        var cfg = ReadFromUi();
        cfg.AlarmEnabled = false;
        cfg.Save();
        Alarm?.Cancel();
        lblAlarm.Text = "알람이 해제되었습니다.";
        Log("알람 해제");
    }

    private void OnExactPerm(object? sender, EventArgs e)
    {
        var svc = Alarm;
        if (svc == null) return;
        if (svc.CanScheduleExact())
        {
            lblAlarm.Text = "정확 알람이 이미 허용된 상태입니다(자동 허용). 설정 목록에 앱이 안 보여도 정상입니다.";
            return;
        }
        // 아직 권한이 없을 때만 시스템 설정 화면 열기
        svc.OpenExactAlarmSettings();
    }

    private void OnTestAlarm(object? sender, EventArgs e)
    {
        var svc = Alarm;
        if (svc == null) return;
        svc.EnsureNotificationPermission();
        svc.ScheduleTest(out var msg);
        lblAlarm.Text = msg + " — 화면을 꺼도 1분 뒤 앱이 떠야 정상";
        Log("테스트 알람 예약: " + msg + " (앱을 닫거나 화면 꺼두고 1분 대기)");
    }

    private void OnBatteryOpt(object? sender, EventArgs e)
    {
        var svc = Alarm;
        if (svc == null) return;
        if (svc.IsIgnoringBatteryOptimizations())
        {
            lblAlarm.Text = "이미 배터리 최적화 예외 상태입니다. (백그라운드 기상 OK)";
            return;
        }
        svc.RequestIgnoreBatteryOptimizations();
    }

    // 알람으로 기동되면 자동 실행
    private bool _updateChecked;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (AutoStart.Requested)
        {
            AutoStart.Requested = false;
            if (AutoStart.TestMode)
            {
                AutoStart.TestMode = false;
                Log("✅ 테스트 알람으로 앱이 깨어남 — 알람 동작 정상");
                await DisplayAlertAsync("테스트 알람 성공",
                    "✅ 알람으로 앱이 정상적으로 깨어났습니다.\n실제 예약일에도 이렇게 깨어나 자동 실행됩니다.", "확인");
                return;
            }
            Log("⏰ 알람으로 자동 시작 — 오픈 시각까지 대기");
            await Task.Delay(800);
            await Run(waitForOpen: true);
            return; // 자동 실행 중엔 업데이트 팝업 띄우지 않음
        }
        if (!_updateChecked)
        {
            _updateChecked = true;
            await CheckUpdateAsync();
        }
    }

    private async Task CheckUpdateAsync()
    {
        var r = await UpdateChecker.CheckAsync();
        if (r is { HasUpdate: true } && !string.IsNullOrEmpty(r.DownloadUrl))
        {
            bool ok = await DisplayAlertAsync("업데이트",
                $"새 버전 v{r.LatestVersion} 이(가) 있습니다. 다운로드할까요?", "다운로드", "나중에");
            if (ok) await Launcher.Default.OpenAsync(r.DownloadUrl);
        }
    }

    private async Task CaptureAsync()
    {
        try
        {
            var shot = await web.CaptureAsync();
            if (shot == null) return;
            // 이전 캡처 정리(누적 방지)
            foreach (var old in Directory.GetFiles(FileSystem.CacheDirectory, "confirm_*.png"))
                try { File.Delete(old); } catch { }
            var path = Path.Combine(FileSystem.CacheDirectory, $"confirm_{DateTime.Now:HHmmss}.png");
            using (var stream = await shot.OpenReadAsync())
            using (var fs = File.Create(path))
                await stream.CopyToAsync(fs);
            imgShot.Source = ImageSource.FromFile(path);
            Log("확인 화면 캡처 저장: " + path);
        }
        catch (Exception ex) { Log("캡처 실패: " + ex.Message); }
    }

    private void SetRunning(bool running)
    {
        btnRunNow.IsEnabled = !running;
        btnSchedule.IsEnabled = !running;
        btnSave.IsEnabled = !running;
        btnStop.IsEnabled = running;
    }

    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _log.AppendLine(line);
            if (_log.Length > 16000) _log.Remove(0, _log.Length - 12000); // 무한 증가 방지
            txtLog.Text = _log.ToString();
        });
    }
}
