using Android.Webkit;
using AWebView = Android.Webkit.WebView;

namespace SMH_android;

/// <summary>
/// 신청하기 후 뜨는 JS 확인/알림창(예약하시겠습니까? / 예약이 완료되었습니다 등)을
/// 자동으로 "확인"하여 예약 절차가 멈추지 않고 끝까지 진행되게 한다.
/// </summary>
public class AutoDialogChromeClient : WebChromeClient
{
    public override bool OnJsConfirm(AWebView? view, string? url, string? message, JsResult? result)
    {
        result?.Confirm();   // "예약하시겠습니까?" → 확인
        return true;
    }

    public override bool OnJsAlert(AWebView? view, string? url, string? message, JsResult? result)
    {
        result?.Confirm();   // "예약이 완료되었습니다" → 확인
        return true;
    }

    public override bool OnJsBeforeUnload(AWebView? view, string? url, string? message, JsResult? result)
    {
        result?.Confirm();
        return true;
    }
}
