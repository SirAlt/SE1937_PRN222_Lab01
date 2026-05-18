using System.Windows;

namespace ChatClient.MVVM.Core;

/* NO LONGER USED */
public static class TextBoxExt
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(TextBoxExt),
            new PropertyMetadata(string.Empty));

    public static void SetPlaceholder(DependencyObject element, string value)
    {
        element.SetValue(PlaceholderProperty, value);
    }

    public static string GetPlaceholder(DependencyObject element)
    {
        return (string)element.GetValue(PlaceholderProperty);
    }

    public static readonly DependencyProperty PlaceholderHorizontalAlignmentProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderHorizontalAlignment",
            typeof(HorizontalAlignment),
            typeof(TextBoxExt),
            new PropertyMetadata(HorizontalAlignment.Left));

    public static void SetPlaceholderHorizontalAlignment(DependencyObject element, HorizontalAlignment value)
    {
        element.SetValue(PlaceholderHorizontalAlignmentProperty, value);
    }

    public static HorizontalAlignment GetPlaceholderHorizontalAlignment(DependencyObject element)
    {
        return (HorizontalAlignment)element.GetValue(PlaceholderHorizontalAlignmentProperty);
    }

    public static readonly DependencyProperty PlaceholderMarginProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderMargin",
            typeof(Thickness),
            typeof(TextBoxExt),
            new PropertyMetadata(new Thickness()));

    public static void SetPlaceholderMargin(DependencyObject element, Thickness value)
    {
        element.SetValue(PlaceholderMarginProperty, value);
    }

    public static Thickness GetPlaceholderMargin(DependencyObject element)
    {
        return (Thickness)element.GetValue(PlaceholderMarginProperty);
    }

    public static readonly DependencyProperty BorderMarginProperty =
        DependencyProperty.RegisterAttached(
            "BorderMargin",
            typeof(Thickness),
            typeof(TextBoxExt),
            new PropertyMetadata(new Thickness()));

    public static void SetBorderMargin(DependencyObject element, Thickness value)
    {
        element.SetValue(BorderMarginProperty, value);
    }

    public static Thickness GetBorderMargin(DependencyObject element)
    {
        return (Thickness)element.GetValue(BorderMarginProperty);
    }

    public static readonly DependencyProperty TextBoxMarginProperty =
        DependencyProperty.RegisterAttached(
            "TextBoxMargin",
            typeof(Thickness),
            typeof(TextBoxExt),
            new PropertyMetadata(new Thickness()));

    public static void SetTextBoxMargin(DependencyObject element, Thickness value)
    {
        element.SetValue(TextBoxMarginProperty, value);
    }

    public static Thickness GetTextBoxMargin(DependencyObject element)
    {
        return (Thickness)element.GetValue(TextBoxMarginProperty);
    }
}
