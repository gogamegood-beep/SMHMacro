namespace SMH_android;

/// <summary>예약 설정 — 기기 Preferences 에 저장.</summary>
public class ResvSettings
{
    public string BaseUrl { get; set; } = "http://www.sangmoohospital.co.kr/reservation/schedule_list.php?co_id=8010";
    public string Type { get; set; } = "초진";          // 초진 / 재진
    public string Applicant { get; set; } = "";
    public bool PatientSameAsApplicant { get; set; } = true;
    public string PatientName { get; set; } = "";
    public string PhoneCarrier { get; set; } = "010";
    public string Phone2 { get; set; } = "";
    public string Phone3 { get; set; } = "";
    public string Password { get; set; } = "";
    public string OpenTime { get; set; } = "20:00";
    public int StartLeadSeconds { get; set; } = 8;
    // 새로고침(폴링) 간격: 매회 [min,max] 사이 랜덤 (사람처럼 / 봇 신호 완화)
    public int PollMinMs { get; set; } = 1000;
    public int PollMaxMs { get; set; } = 5000;
    // 폼 입력 사이 지연: [min,max] 사이 랜덤
    public int InputMinMs { get; set; } = 200;
    public int InputMaxMs { get; set; } = 500;
    public int MaxPollMinutes { get; set; } = 6;
    public bool AutoScan { get; set; } = true;
    public int ScanYear { get; set; } = DateTime.Now.Year;
    public int ScanMonth { get; set; } = DateTime.Now.Month;
    public bool DryRun { get; set; } = true;

    // 알람: 오픈 N분 전에 깨움 (기본 5분 → 20:00 오픈이면 19:55 알람)
    public int AlarmLeadMinutes { get; set; } = 5;
    public bool AlarmEnabled { get; set; } = false;

    public static ResvSettings Load() => new()
    {
        Type = Preferences.Get(nameof(Type), "초진"),
        Applicant = Preferences.Get(nameof(Applicant), ""),
        PatientSameAsApplicant = Preferences.Get(nameof(PatientSameAsApplicant), true),
        PatientName = Preferences.Get(nameof(PatientName), ""),
        PhoneCarrier = Preferences.Get(nameof(PhoneCarrier), "010"),
        Phone2 = Preferences.Get(nameof(Phone2), ""),
        Phone3 = Preferences.Get(nameof(Phone3), ""),
        Password = Preferences.Get(nameof(Password), ""),
        OpenTime = Preferences.Get(nameof(OpenTime), "20:00"),
        PollMinMs = Preferences.Get(nameof(PollMinMs), 1000),
        PollMaxMs = Preferences.Get(nameof(PollMaxMs), 5000),
        InputMinMs = Preferences.Get(nameof(InputMinMs), 200),
        InputMaxMs = Preferences.Get(nameof(InputMaxMs), 500),
        MaxPollMinutes = Preferences.Get(nameof(MaxPollMinutes), 6),
        AutoScan = Preferences.Get(nameof(AutoScan), true),
        ScanYear = Preferences.Get(nameof(ScanYear), DateTime.Now.Year),
        ScanMonth = Preferences.Get(nameof(ScanMonth), DateTime.Now.Month),
        DryRun = Preferences.Get(nameof(DryRun), true),
        AlarmLeadMinutes = Preferences.Get(nameof(AlarmLeadMinutes), 5),
        AlarmEnabled = Preferences.Get(nameof(AlarmEnabled), false),
    };

    public void Save()
    {
        Preferences.Set(nameof(Type), Type);
        Preferences.Set(nameof(Applicant), Applicant);
        Preferences.Set(nameof(PatientSameAsApplicant), PatientSameAsApplicant);
        Preferences.Set(nameof(PatientName), PatientName);
        Preferences.Set(nameof(PhoneCarrier), PhoneCarrier);
        Preferences.Set(nameof(Phone2), Phone2);
        Preferences.Set(nameof(Phone3), Phone3);
        Preferences.Set(nameof(Password), Password);
        Preferences.Set(nameof(OpenTime), OpenTime);
        Preferences.Set(nameof(PollMinMs), PollMinMs);
        Preferences.Set(nameof(PollMaxMs), PollMaxMs);
        Preferences.Set(nameof(InputMinMs), InputMinMs);
        Preferences.Set(nameof(InputMaxMs), InputMaxMs);
        Preferences.Set(nameof(MaxPollMinutes), MaxPollMinutes);
        Preferences.Set(nameof(AutoScan), AutoScan);
        Preferences.Set(nameof(ScanYear), ScanYear);
        Preferences.Set(nameof(ScanMonth), ScanMonth);
        Preferences.Set(nameof(DryRun), DryRun);
        Preferences.Set(nameof(AlarmLeadMinutes), AlarmLeadMinutes);
        Preferences.Set(nameof(AlarmEnabled), AlarmEnabled);
    }
}
