using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ProductInformationManager.Web.Notifiers;

public class CategoryNotifier : INotifier, IDisposable
{
    private readonly Subject<CategoryChangeNotification> _changes = new();

    public IObservable<CategoryChangeNotification> Changes => _changes.AsObservable();

    public void Notify(CategoryChangeNotification change) => _changes.OnNext(change);

    public void Dispose()
    {
        _changes.Dispose();
        GC.SuppressFinalize(this);
    }
}
