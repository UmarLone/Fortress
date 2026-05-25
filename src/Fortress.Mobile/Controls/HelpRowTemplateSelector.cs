using Fortress.Models;

namespace Fortress.Controls
{
    /// <summary>
    /// Picks the right DataTemplate for each <see cref="HelpRow"/> kind
    /// in the HelpPage CollectionView, enabling full virtualisation.
    /// Templates are defined as resources on HelpPage and injected via XAML properties.
    /// </summary>
    public class HelpRowTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? CategoryHeaderTemplate { get; set; }
        public DataTemplate? QuestionTemplate       { get; set; }
        public DataTemplate? FooterTemplate         { get; set; }

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
       => item is HelpRow row
 ? row.Kind switch
   {
    HelpRowKind.CategoryHeader => CategoryHeaderTemplate,
 HelpRowKind.Question       => QuestionTemplate,
      HelpRowKind.Footer     => FooterTemplate,
      _                  => null,
    }
 : null;
    }
}
