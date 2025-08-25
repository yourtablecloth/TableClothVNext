using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TableCloth3.Shared.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    public BaseViewModel()
        : base()
    {
        if (IsDesignMode)
            PrepareDesignTimePreview();
    }

    protected virtual void PrepareDesignTimePreview() { }

    protected bool IsDesignMode => Design.IsDesignMode;
}
