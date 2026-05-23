using Microsoft.Xaml.Behaviors;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace ChatClient.MVVM.Behaviors;

public class ScrollToNewItemBehavior : Behavior<ListBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject.ItemsSource is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged += OnNewItemAdded;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject.ItemsSource is INotifyCollectionChanged incc)
        {
            incc.CollectionChanged -= OnNewItemAdded;
        }
    }

    private void OnNewItemAdded(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var newestItem = e.NewItems?[e.NewItems.Count - 1];
            AssociatedObject.ScrollIntoView(newestItem);
        }
    }
}
