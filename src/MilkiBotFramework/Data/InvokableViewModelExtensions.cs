using System.Runtime.CompilerServices;

namespace MilkiBotFramework.Data;

public static class InvokableViewModelExtensions
{
    public static TRet RaiseAndSetIfChanged<TObj, TRet>(
        this TObj reactiveObject,
        ref TRet backingField,
        TRet newValue,
        [CallerMemberName] string? propertyName = null, params string[] additionalChangedMembers)
        where TObj : IInvokableViewModel
    {
        if (propertyName is null)
        {
            throw new ArgumentNullException(nameof(propertyName));
        }

        if (EqualityComparer<TRet>.Default.Equals(backingField, newValue))
        {
            return newValue;
        }

        reactiveObject.RaisePropertyChanging(propertyName);
        backingField = newValue;
        reactiveObject.RaisePropertyChanged(propertyName);
        foreach (var member in additionalChangedMembers)
        {
            reactiveObject.RaisePropertyChanged(member);
        }

        return newValue;
    }
}