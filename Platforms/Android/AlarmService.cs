using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AApplication = Android.App.Application;

namespace SMH_android;

public class AlarmService : IAlarmService
{
    private const int RequestCode = 7788;

    // 예약창이 열리는 요일: 목/금/일/월
    private static readonly DayOfWeek[] OpenDays =
    {
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Sunday, DayOfWeek.Monday
    };

    private static Context Ctx => AApplication.Context;
    private static AlarmManager Am => (AlarmManager)Ctx.GetSystemService(Context.AlarmService)!;

    public bool CanScheduleExact()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            return Am.CanScheduleExactAlarms();
        return true;
    }

    public void OpenExactAlarmSettings()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return;
        var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
        intent.AddFlags(ActivityFlags.NewTask);
        Ctx.StartActivity(intent);
    }

    public DateTime? ScheduleNext(out string message)
    {
        var cfg = ResvSettings.Load();
        if (!TimeSpan.TryParse(cfg.OpenTime, out var open))
        {
            message = "오픈 시각 형식 오류";
            return null;
        }
        if (!CanScheduleExact())
        {
            message = "정확 알람 권한이 꺼져 있습니다. '권한 설정'에서 허용하세요.";
            return null;
        }

        var alarmTod = open - TimeSpan.FromMinutes(cfg.AlarmLeadMinutes);
        var when = NextOccurrence(DateTime.Now, alarmTod);

        Am.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, ToEpochMillis(when), BuildPendingIntent());
        message = $"알람 예약: {when:M월 d일(ddd) HH:mm} (오픈 {cfg.AlarmLeadMinutes}분 전)";
        return when;
    }

    public DateTime? ScheduleTest(out string message)
    {
        if (!CanScheduleExact())
        {
            message = "정확 알람 권한이 꺼져 있습니다. '정확 알람 확인'을 눌러 허용하세요.";
            return null;
        }
        var when = DateTime.Now.AddSeconds(60);
        Am.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, ToEpochMillis(when), BuildPendingIntent(test: true));
        message = $"테스트 알람: {when:HH:mm:ss} (1분 뒤)";
        return when;
    }

    public void EnsureNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return;
        var act = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (act == null) return;
        const string perm = "android.permission.POST_NOTIFICATIONS";
        if (act.CheckSelfPermission(perm) != Android.Content.PM.Permission.Granted)
            act.RequestPermissions(new[] { perm }, 1001);
    }

    public void Cancel() => Am.Cancel(BuildPendingIntent());

    public bool IsIgnoringBatteryOptimizations()
    {
        var pm = (PowerManager)Ctx.GetSystemService(Context.PowerService)!;
        return pm.IsIgnoringBatteryOptimizations(Ctx.PackageName!);
    }

    public void RequestIgnoreBatteryOptimizations()
    {
        if (IsIgnoringBatteryOptimizations()) return;
        var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
        intent.SetData(Android.Net.Uri.Parse("package:" + Ctx.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        Ctx.StartActivity(intent);
    }

    private static PendingIntent BuildPendingIntent(bool test = false)
    {
        var intent = new Intent(Ctx, typeof(AlarmReceiver));
        intent.PutExtra("test", test);
        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        return PendingIntent.GetBroadcast(Ctx, test ? RequestCode + 1 : RequestCode, intent, flags)!;
    }

    private static DateTime NextOccurrence(DateTime now, TimeSpan tod)
    {
        for (int i = 0; i < 14; i++)
        {
            var d = now.Date.AddDays(i) + tod;
            if (Array.IndexOf(OpenDays, d.DayOfWeek) >= 0 && d > now)
                return d;
        }
        return now.AddMinutes(1);
    }

    private static long ToEpochMillis(DateTime localTime)
    {
        var utc = localTime.ToUniversalTime();
        return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }
}
