using Microsoft.Maui.Controls.Shapes;

namespace SMH_android;

/// <summary>흰 박스 + 검은 체크(✔)의 고대비 커스텀 체크박스. IsChecked(bool) 호환.</summary>
public class CheckBoxView : ContentView
{
    private readonly Label _check;

    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        nameof(IsChecked), typeof(bool), typeof(CheckBoxView), false,
        BindingMode.TwoWay, propertyChanged: (b, _, _) => ((CheckBoxView)b).Update());

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public event EventHandler<CheckedChangedEventArgs>? CheckedChanged;

    public CheckBoxView()
    {
        _check = new Label
        {
            Text = "✔",
            TextColor = Colors.Black,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };

        var box = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Colors.Black,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            WidthRequest = 34,
            HeightRequest = 34,
            Padding = 0,
            Content = _check,
        };
        box.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => IsChecked = !IsChecked),
        });

        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;
        Content = box;
    }

    private void Update()
    {
        _check.IsVisible = IsChecked;
        CheckedChanged?.Invoke(this, new CheckedChangedEventArgs(IsChecked));
    }
}
