using System;
using System.Windows;
using System.Windows.Media;

namespace Wilt.Controls;

/// <summary>
/// Thin circular progress ring: remaining time depletes the ring, echoing
/// the character's Energy depletion (PRD 9.1.1).
/// </summary>
public partial class ProgressRing : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(0.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(120.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ProgressRing),
        new PropertyMetadata(4.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ProgressRing),
        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush), typeof(Brush), typeof(ProgressRing),
        new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x8B, 0xC3, 0xA8))));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public ProgressRing()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ProgressRing)d).Redraw();
    }

    private void Redraw()
    {
        if (ArcPath == null || Diameter <= 0)
        {
            return;
        }

        var radius = Diameter / 2.0;
        var center = new Point(radius, radius);
        var angle = 360.0 * Math.Clamp(Progress, 0.0001, 0.9999);
        var startPoint = new Point(center.X, center.Y - radius + Thickness / 2);

        var angleRad = (angle - 90) * Math.PI / 180.0;
        var effectiveRadius = radius - Thickness / 2;
        var endPoint = new Point(
            center.X + effectiveRadius * Math.Cos(angleRad),
            center.Y + effectiveRadius * Math.Sin(angleRad));

        var isLargeArc = angle > 180.0;

        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment(endPoint, new Size(effectiveRadius, effectiveRadius), 0, isLargeArc,
            SweepDirection.Clockwise, isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ArcPath.Data = geometry;
        Width = Diameter;
        Height = Diameter;
    }
}
