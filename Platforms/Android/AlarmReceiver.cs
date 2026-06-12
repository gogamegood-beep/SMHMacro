using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace SMH_android;

/// <summary>
/// 정시 알람 수신기. 오픈 N분 전에 깨어나:
///  - 풀스크린 인텐트 알림으로 MainActivity 기동(잠금화면에서도)
///  - 자동 실행 플래그 설정
///  - 다음 회차 알람 재예약(정확 알람은 1회성이므로)
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class AlarmReceiver : BroadcastReceiver
{
    private const string ChannelId = "resv_alarm";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        bool test = intent?.GetBooleanExtra("test", false) ?? false;
        AutoStart.Requested = true;
        AutoStart.TestMode = test;

        var launch = new Intent(context, typeof(MainActivity));
        launch.PutExtra("autostart", true);
        launch.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);

        var piFlags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        var contentPi = PendingIntent.GetActivity(context, 1001, launch, piFlags);

        var nm = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var ch = new NotificationChannel(ChannelId, "예약 알람", NotificationImportance.High);
            nm.CreateNotificationChannel(ch);
        }

        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetContentTitle(test ? "테스트 알람" : "상무병원 예약 준비");
        builder.SetContentText(test ? "알람으로 앱이 깨어났습니다." : "예약 자동 실행을 시작합니다.");
        builder.SetSmallIcon(Android.Resource.Drawable.IcDialogInfo);
        builder.SetPriority((int)NotificationPriority.High);
        builder.SetCategory(NotificationCompat.CategoryAlarm);
        builder.SetFullScreenIntent(contentPi, true);
        builder.SetAutoCancel(true);
        nm.Notify(2001, builder.Build());

        // 폴백: 액티비티 직접 기동 시도
        try { context.StartActivity(launch); } catch { /* 백그라운드 제한 시 알림으로 대체 */ }

        // 실제 알람만 다음 회차 재예약(테스트는 제외)
        if (!test)
            try { new AlarmService().ScheduleNext(out _); } catch { }
    }
}
