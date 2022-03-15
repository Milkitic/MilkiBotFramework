using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MilkiBotFramework.Data;

/// <summary>
/// ViewModel基础类
/// </summary>
public abstract class ViewModelBase : IInvokableViewModel, INotifyPropertyChanged, INotifyPropertyChanging
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event PropertyChangingEventHandler? PropertyChanging;

    /// <summary>
    /// 通知UI更新操作
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void IInvokableViewModel.RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    void IInvokableViewModel.RaisePropertyChanging(string propertyName)
    {
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
    }
}