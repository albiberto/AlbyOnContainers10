using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ProductInformationManager.Web.Notifiers;

public interface INotifier;

public abstract class NotifierBase : INotifier, IDisposable
{
    private readonly Subject<Guid> _changes = new();
    
    public IObservable<Guid> Changes => _changes.AsObservable();

    public void NotifyChanged(Guid entityId) => _changes.OnNext(entityId);

    public void Dispose()
    {
        _changes.Dispose();
        GC.SuppressFinalize(this);
    }
}