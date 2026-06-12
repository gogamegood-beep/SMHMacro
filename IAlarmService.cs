namespace SMH_android;

/// <summary>정시 알람(플랫폼별 구현). 오픈 N분 전에 앱을 깨운다.</summary>
public interface IAlarmService
{
    /// <summary>다음 진료일(목/금/일/월) 오픈-리드분 시각에 정확 알람 예약. 성공 시 예약시각 반환.</summary>
    DateTime? ScheduleNext(out string message);

    /// <summary>예약된 알람 취소.</summary>
    void Cancel();

    /// <summary>정확 알람(Exact Alarm) 권한 가능 여부(Android 12+).</summary>
    bool CanScheduleExact();

    /// <summary>정확 알람 권한 설정 화면 열기(필요 시).</summary>
    void OpenExactAlarmSettings();

    /// <summary>배터리 최적화에서 제외돼 있는지(백그라운드 기상 안정성).</summary>
    bool IsIgnoringBatteryOptimizations();

    /// <summary>배터리 최적화 예외 요청 다이얼로그 열기.</summary>
    void RequestIgnoreBatteryOptimizations();
}

/// <summary>알람 수신 → 앱 기동 시 자동 실행 요청 플래그.</summary>
public static class AutoStart
{
    public static bool Requested;
}
