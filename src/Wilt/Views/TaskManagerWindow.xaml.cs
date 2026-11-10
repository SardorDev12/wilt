using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wilt.Models;
using Wilt.Services;

namespace Wilt.Views;

/// <summary>
/// Full CRUD over every task regardless of date (Settings -> Manage Tasks),
/// unlike the overlay's Task Mode list which only ever shows today's plan
/// plus a lightweight missed-task triage.
/// </summary>
public partial class TaskManagerWindow : Window
{
    private readonly TaskEngine _taskEngine;
    private readonly ObservableCollection<WorkTask> _items;
    private bool _suppressReload;

    public TaskManagerWindow(TaskEngine taskEngine)
    {
        InitializeComponent();
        _taskEngine = taskEngine;

        _items = new ObservableCollection<WorkTask>(_taskEngine.AllTasks);
        TasksGrid.ItemsSource = _items;
        NewDatePicker.SelectedDate = DateTime.Today;

        _taskEngine.TasksChanged += OnTasksChangedElsewhere;
        Closed += (_, _) => _taskEngine.TasksChanged -= OnTasksChangedElsewhere;
    }

    private void OnTasksChangedElsewhere(object? sender, EventArgs e)
    {
        if (_suppressReload)
        {
            return;
        }

        Dispatcher.Invoke(Reload);
    }

    private void Reload()
    {
        _items.Clear();
        foreach (var task in _taskEngine.AllTasks)
        {
            _items.Add(task);
        }
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is not WorkTask task)
        {
            return;
        }

        // The grid hasn't pushed the edited value into the bound object yet
        // at the moment this event fires - defer the persist until after it has.
        Dispatcher.BeginInvoke(new Action(() => Persist(task)));
    }

    private void Persist(WorkTask task)
    {
        _suppressReload = true;
        _taskEngine.UpdateTask(task.Id, task.Name, task.PlannedMinutes, task.Date, task.IsDone);
        _suppressReload = false;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var name = NewNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || !int.TryParse(NewMinutesBox.Text, out var minutes) || minutes <= 0)
        {
            return;
        }

        var date = NewDatePicker.SelectedDate ?? DateTime.Today;

        _suppressReload = true;
        _taskEngine.AddTask(name, minutes, date);
        _suppressReload = false;

        NewNameBox.Text = "";
        NewMinutesBox.Text = "";
        NewDatePicker.SelectedDate = DateTime.Today;

        Reload();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkTask task })
        {
            return;
        }

        _suppressReload = true;
        _taskEngine.DeleteTask(task.Id);
        _suppressReload = false;

        _items.Remove(task);
    }

    private void OnMinutesPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }
}
