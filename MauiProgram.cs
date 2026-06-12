using Microsoft.Extensions.Logging;

namespace SMH_android;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID
		builder.Services.AddSingleton<IAlarmService, AlarmService>();

		// WebView 를 데스크톱 모드로 고정 → PC 와 동일한 레이아웃/구조를 받게 함
		Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("DesktopMode", (handler, view) =>
		{
			var settings = handler.PlatformView.Settings;
			settings.UserAgentString =
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
			settings.UseWideViewPort = true;
			settings.LoadWithOverviewMode = true;
			settings.JavaScriptEnabled = true;
			settings.DomStorageEnabled = true;
			// 신청 후 JS 확인/알림창 자동 확인
			handler.PlatformView.SetWebChromeClient(new AutoDialogChromeClient());
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
