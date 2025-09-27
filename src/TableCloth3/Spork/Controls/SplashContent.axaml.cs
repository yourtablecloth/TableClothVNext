using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace TableCloth3.Spork.Controls;

public partial class SplashContent : UserControl
{
    public SplashContent()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        if (IsVisible)
            StartAnimation().SafeFireAndForget();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (Design.IsDesignMode)
            StartAnimation().SafeFireAndForget();
    }

    public async Task StartAnimation()
    {
        var imageStartOffsetY = -700.0d;
        var textStartOffsetY = -50.0d;
        var easeOut = new CubicEaseOut();

        var imageAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5d),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, imageStartOffsetY),
                        new Setter(Visual.OpacityProperty, 0.0)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, 0d),
                        new Setter(Visual.OpacityProperty, 1d)
                    }
                }
            }
        };

        var textAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5d),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame
                {
                    Cue     = new Cue(0d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, textStartOffsetY),
                        new Setter(Visual.OpacityProperty, 0d)
                    }
                },
                new KeyFrame
                {
                    Cue     = new Cue(0.5d),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, textStartOffsetY),
                        new Setter(Visual.OpacityProperty, 0d)
                    }
                },
                new KeyFrame
                {
                    Cue     = new Cue(1.0),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, 0d),
                        new Setter(Visual.OpacityProperty, 1d)
                    }
                }
            }
        };

        await Task.WhenAll(imageAnim.RunAsync(image), textAnim.RunAsync(text));

        var fadeOutAnim = new Animation
        {
            FillMode = FillMode.Forward,
            Duration = TimeSpan.FromSeconds(0.5d),
            Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Visual.OpacityProperty, 1d) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(Visual.OpacityProperty, 0d) }
                    }
                }
        };

        await Task.WhenAll(fadeOutAnim.RunAsync(image), fadeOutAnim.RunAsync(text), fadeOutAnim.RunAsync(this));

        if (Parent is Panel panel)
            panel.Children.Remove(this);
    }
}
