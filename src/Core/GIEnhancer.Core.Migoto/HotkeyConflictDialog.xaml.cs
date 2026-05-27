using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;

namespace GIEnhancer.Core.Migoto;

public partial class HotkeyConflictDialog : Window
{
    private readonly List<HotkeyConflict> _conflicts;
    private bool _applied;

    /// <summary>
    /// 用户是否点击了"应用并启动"
    /// </summary>
    public bool Applied => _applied;

    /// <summary>
    /// 冲突解决结果列表
    /// </summary>
    public List<HotkeyConflict> ResolvedConflicts => _conflicts;

    public HotkeyConflictDialog(List<HotkeyConflict> conflicts)
    {
        _conflicts = conflicts;
        InitializeComponent();
        ConflictList.ItemsSource = _conflicts;

        // 为每个冲突初始化 ComboBox
        Loaded += (_, _) =>
        {
            foreach (var conflict in _conflicts)
            {
                var panel = FindVisualChild<StackPanel>(ConflictList.ItemContainerGenerator.ContainerFromItem(conflict));
                if (panel == null) continue;

                var combo = panel.Children.OfType<ComboBox>().FirstOrDefault(c => c.Tag == conflict);
                if (combo == null) continue;

                // 填充替代键选项
                foreach (var (vk, name) in HotkeyConflictResolver.SuggestedReplacementKeys)
                {
                    if (vk != conflict.VirtualKey)
                        combo.Items.Add(new ComboBoxItem { Content = name, Tag = vk });
                }

                // 默认选中第一个安全替代键
                if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
            }
        };
    }

    private void OnModifyTargetChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.DataContext is HotkeyConflict conflict)
            conflict.ModifyTarget = rb.Tag?.ToString() ?? "migoto";
    }

    private void OnReplaceKeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item
            && combo.Tag is HotkeyConflict conflict)
        {
            conflict.ReplaceWithVk = (int)(item.Tag ?? 0);
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        _applied = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _applied = false;
        Close();
    }

    // 辅助：查找可视化子元素
    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
