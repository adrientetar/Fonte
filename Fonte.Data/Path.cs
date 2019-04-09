﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.Data
{
    using Fonte.Data.Changes;
    using Fonte.Data.Interfaces;
    using Fonte.Data.Utilities;
    using Microsoft.Graphics.Canvas;
    using Microsoft.Graphics.Canvas.Geometry;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using Windows.Foundation;

    [JsonConverter(typeof(PathConverter))]
    public partial class Path
    {
        internal List<Point> _points;

        internal Dictionary<string, object> _extraData;

        private Rect _bounds = Rect.Empty;
        private CanvasGeometry _canvasPath;

        public ObserverList<Point> Points
        {
            get
            {
                var items = new ObserverList<Point>(_points);
                items.CollectionChanged += (sender, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        if (e.NewItems.Count > 1)
                        {
                            new PathPointsRangeChange(this, e.NewStartingIndex, e.NewItems.Cast<Point>().ToList(), true).Apply();
                        }
                        else
                        {
                            new PathPointsChange(this, e.NewStartingIndex, (Point)e.NewItems[0]).Apply();
                        }
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Remove)
                    {
                        if (e.OldItems.Count > 1)
                        {
                            new PathPointsRangeChange(this, e.OldStartingIndex, e.OldItems.Cast<Point>().ToList(), false).Apply();
                        }
                        else
                        {
                            new PathPointsChange(this, e.OldStartingIndex, null).Apply();
                        }
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Replace)
                    {
                        Debug.Assert(e.NewItems.Count == 1);

                        new PathPointsReplaceChange(this, e.NewStartingIndex, (Point)e.NewItems[0]).Apply();
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Reset)
                    {
                        new PathPointsResetChange(this).Apply();
                    }
                };
                return items;
            }
        }

        public IReadOnlyDictionary<string, object> ExtraData
        {
            get
            {
                if (_extraData == null)
                {
                    _extraData = new Dictionary<string, object>();
                }
                return _extraData;
            }
        }

        public Layer Parent
        { get; internal set; }

        /**/

        public Rect Bounds
        {
            get
            {
                if (_bounds.IsEmpty)
                {
                    _bounds = CanvasPath.ComputeBounds();
                }
                return _bounds;
            }
        }

        public CanvasGeometry CanvasPath
        {
            get
            {
                if (_canvasPath == null)
                {
                    var device = CanvasDevice.GetSharedDevice();
                    var builder = new CanvasPathBuilder(device);

                    var stack = new Vector2[2];
                    var stackIndex = 0;

                    var start = Points[0];
                    var skip = start.Type == PointType.Move;
                    if (!skip)
                    {
                        start = Points[Points.Count - 1];
                    }
                    builder.BeginFigure(start.X, start.Y);

                    foreach (var point in Points)
                    {
                        if (skip)
                        {
                            skip = false;
                            continue;
                        }
                        switch (point.Type)
                        {
                            case PointType.Curve:
                                Debug.Assert(stackIndex == 2);
                                builder.AddCubicBezier(stack[0], stack[1], point.ToVector2());
                                stackIndex = 0;
                                break;
                            case PointType.Line:
                                builder.AddLine(point.X, point.Y);
                                break;
                            case PointType.None:
                                stack[stackIndex++] = point.ToVector2();
                                break;
                        }
                    }

                    builder.EndFigure(start.Type == PointType.Move ? CanvasFigureLoop.Open : CanvasFigureLoop.Closed);

                    _canvasPath = CanvasGeometry.CreatePath(builder);
                }
                return _canvasPath;
            }
        }

        public bool IsOpen
        {
            get
            {
                return Points.Count == 0 || Points[0].Type == PointType.Move;
            }
        }

        public bool Selected
        {
            get
            {
                foreach (Point point in Points)
                {
                    if (!point.Selected) return false;
                }
                return true;
            }
        }

        public IEnumerable<Segment> Segments
        {
            get
            {
                var points = Points;

                int start = 0, count = 0;
                foreach (var point in points)
                {
                    ++count;
                    if (point.Type != PointType.None)
                    {
                        yield return new Segment(points, start, count);
                        start += count;
                        count = 0;
                    }
                }
            }
        }

        public Path(List<Point> points = null, Dictionary<string, object> extraData = null)
        {
            _points = points ?? new List<Point>();
            _extraData = extraData;

            foreach (Point point in Points)
            {
                //point.Selected = false;
                point.Parent = this;
            }
        }

        public void Close()
        {
            if (Points.Count > 0 && IsOpen)
            {
                var point = Points.PopAt(0);
                Points.Add(point);
                point.Smooth = false;
                point.Type = PointType.Line;
            }
        }

        public void Reverse()
        {
            if (Points.Count > 0)
            {
                var start = Points[0];
                var type = start.Type;
                if (type != PointType.Move)
                {
                    var pivot = Points.Pop();
                    Points.Reverse();
                    Points.Add(pivot);
                    type = pivot.Type;
                }
                else
                {
                    Points.Reverse();
                }

                foreach (var point in Points)
                {
                    if (point.Type != PointType.None)
                    {
                        var pType = point.Type;
                        point.Type = type;
                        type = pType;
                    }
                }
            }
        }

        public void StartAt(int index)
        {
            if (IsOpen)
            {
                throw new InvalidOperationException("Cannot set start point in open path");
            }
            if (Points.Count - index + 1 != 0)
            {
                var end = Points.GetRange(0, index + 1);
                if (end.Count > 0 && end[end.Count - 1].Type == PointType.None)
                {
                    throw new InvalidOperationException($"Index {index} breaks a segment");
                }
                Points.RemoveRange(0, index + 1);
                Points.AddRange(end);
            }
        }

        public override string ToString()
        {
            return $"{nameof(Path)}({Points})";
        }

        public void Transform(Matrix3x2 matrix)
        {
            throw new NotImplementedException();
        }

        internal void OnChange(IChange change)
        {
            _bounds = Rect.Empty;
            _canvasPath = null;

            Parent?.OnChange(change);
        }
    }

    public struct Segment
    {
        private readonly int _count;
        private readonly int _index;
        private ObserverList<Point> _points;

        public List<Point> OffCurves
        {
            get
            {
                return _points.GetRange(_index, _count - 1);
            }
        }

        public Point OnCurve
        {
            get
            {
                return _points[_index + _count - 1];
            }
        }

        public List<Point> Points
        {
            get
            {
                return _points.GetRange(_index, _count);
            }
        }

        public List<Point> PointsInclusive
        {
            get
            {
                var index = _index - (OnCurve.Type != PointType.Move ? 1 : 0);
                /* Start point on the other end */
                if (index < 0)
                {
                    var list = _points.GetRange(index, _count);
                    list.Insert(0, _points[_points.Count - 1]);
                    return list;
                }
                return _points.GetRange(index, _count);
            }
        }

        public Segment(ObserverList<Point> points, int index, int count)
        {
            _count = count;
            _index = index;
            _points = points;
        }

        public void ConvertTo(PointType type)
        {
            bool ok = type == OnCurve.Type;
            if (type == PointType.Curve)
            {
                if (OnCurve.Type == PointType.Line)
                {
                    var start = PointsInclusive[0];
                    var index = _index + _count;
                    _points.Insert(index, new Point(
                            start.X + .65f * (OnCurve.X - start.X),
                            start.Y + .65f * (OnCurve.Y - start.Y)
                        ));
                    _points.Insert(index, new Point(
                            start.X + .35f * (OnCurve.X - start.X),
                            start.Y + .35f * (OnCurve.Y - start.Y)
                        ));
                    OnCurve.Type = PointType.Curve;

                    ok = true;
                }
            }
            if (type == PointType.Line)
            {
                if (OnCurve.Type == PointType.Curve)
                {
                    _points.RemoveRange(_index, _count - 1);
                    var start = _points.First();
                    start.Smooth = false;
                    start.Type = PointType.Line;

                    ok = true;
                }
            }
            else if (type == PointType.Move)
            {
                if (OnCurve.Type == PointType.Curve ||
                    OnCurve.Type == PointType.Line)
                {
                    if (_index != 0)
                    {
                        throw new InvalidOperationException(
                            string.Format("Segment for conversion to {0} needs to be at index 0 ({1})", type, _index));
                    }
                    _points.RemoveRange(_index, _count - 1);
                    var start = _points.First();
                    start.Smooth = false;
                    start.Type = PointType.Move;

                    ok = true;
                }
            }

            if (!ok)
            {
                throw new InvalidOperationException(
                    string.Format("Cannot convert from {0} to {1}", OnCurve.Type, type));
            }
        }
    }

    internal class PathConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Path).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);

            var last = array.Last;
            Dictionary<string, object> extraData = null;
            if (last.Type == JTokenType.Object)
            {
                extraData = last.ToObject<Dictionary<string, object>>();
                array.RemoveAt(array.Count - 1);
            }

            return new Path(
                    array.ToObject<List<Point>>(),
                    extraData
                );
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var path = (Path)value;

            IEnumerable<object> data;
            if (path.ExtraData != null && path.ExtraData.Count > 0)
            {
                var content = new List<object>(path.Points.Count + 1);
                content.AddRange(path._points);
                content.Add(path.ExtraData);
                data = content;
            }
            else
            {
                data = path._points;
            }
            serializer.Serialize(writer, data);
        }
    }
}
