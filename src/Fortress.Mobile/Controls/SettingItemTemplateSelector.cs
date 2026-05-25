using Fortress.Models;

namespace Fortress.Controls
{
    /// <summary>
    /// Picks the right DataTemplate for each <see cref="SettingItem"/> kind
    /// in the settings CollectionView.
    /// Templates are defined as resources on MenuPage and injected via XAML properties.
    /// </summary>
    public class SettingItemTemplateSelector : DataTemplateSelector
    {
 public DataTemplate? SectionHeaderTemplate { get; set; }
        public DataTemplate? ToggleTemplate { get; set; }
        public DataTemplate? NavTemplate { get; set; }
        public DataTemplate? SpacerTemplate { get; set; }

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
     => item is SettingItem s
            ? s.Kind switch
          {
    SettingItemKind.SectionHeader => SectionHeaderTemplate,
           SettingItemKind.Toggle => ToggleTemplate,
        SettingItemKind.Nav => NavTemplate,
         SettingItemKind.Spacer => SpacerTemplate,
    _ => null
  }
     : null;
    }
}
