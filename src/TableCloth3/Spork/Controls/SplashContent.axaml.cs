using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using System.Diagnostics;

namespace TableCloth3.Spork.Controls;

public partial class SplashContent : UserControl
{
    public SplashContent()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        if (IsVisible)
        {
            await StartAnimation();
        }
    }

    public Func<Task>? InitializationTask { get; set; }

    protected async override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (Design.IsDesignMode)
        {
            await StartAnimation();
        }
    }

    public async Task StartAnimation()
    {
        double imageStartOffsetY = -700.0;
        double textStartOffsetY = -50.0;

        var easeOut = new CubicEaseOut();

        var imageAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0.0),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, imageStartOffsetY),
                        new Setter(Visual.OpacityProperty, 0.0)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1.0),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, 0.0),
                        new Setter(Visual.OpacityProperty, 1.0)
                    }
                }
            }
        };

        var textAnim = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.5),
            FillMode = FillMode.Forward,
            Easing = easeOut,
            Children =
            {
                new KeyFrame
                {
                    Cue     = new Cue(0.0),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, textStartOffsetY),
                        new Setter(Visual.OpacityProperty, 0.0)
                    }
                },
                new KeyFrame
                {
                    Cue     = new Cue(0.5),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, textStartOffsetY),
                        new Setter(Visual.OpacityProperty,      0.0)
                    }
                },
                new KeyFrame
                {
                    Cue     = new Cue(1.0),
                    Setters =
                    {
                        new Setter(TranslateTransform.YProperty, 0.0),
                        new Setter(Visual.OpacityProperty, 1.0)
                    }
                }
            }
        };

        await Task.WhenAll(imageAnim.RunAsync(image), textAnim.RunAsync(text));

        if (InitializationTask is not null)
        {
            await InitializationTask();

            InitializationTask = null;
        }
        else
        {
            if (!Debugger.IsAttached)
            {
                await Task.Delay(1500);
            }
        }

        var fadeOutAnim = new Animation
        {
            FillMode = FillMode.Forward,
            Duration = TimeSpan.FromSeconds(0.5),
            Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                    }
                }
        };

        await Task.WhenAll(fadeOutAnim.RunAsync(image), fadeOutAnim.RunAsync(text), fadeOutAnim.RunAsync(this));

        if (Parent is Panel panel)
        {
            panel.Children.Remove(this);
        }
    }
}
