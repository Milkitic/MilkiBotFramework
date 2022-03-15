namespace MilkiBotFramework.Data;

public interface IInvokableViewModel
{
    internal void RaisePropertyChanged(string propertyName);
    internal void RaisePropertyChanging(string propertyName);
}