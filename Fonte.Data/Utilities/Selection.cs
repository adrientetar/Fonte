﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.Data.Utilities
{
    using System.Collections.Generic;

    public class Selection
    {
        public static List<Path> FilterSelection(IEnumerable<Path> paths, bool invert = false)
        {
            var selValue = !invert;
            var outPaths = new List<Path>();
            foreach (var path in paths)
            {
                var segments = path.Segments;
                Segment firstSegment;
                {
                    var enumerator = segments.GetEnumerator();
                    enumerator.MoveNext();
                    firstSegment = enumerator.Current;
                }

                IEnumerable<Segment> iter;
                if (!path.IsOpen && firstSegment.OnCurve.Selected == selValue)
                {
                    var segmentsList = new List<Segment>(segments);

                    int index;
                    for (index = segmentsList.Count - 1; index >= 0; --index)
                    {
                        if (segmentsList[index].OnCurve.Selected != selValue)
                        {
                            break;
                        }
                    }
                    if (index <= 0)
                    {
                        // All selected, bring on the original path
                        outPaths.Add(path);
                        continue;
                    }
                    else
                    {
                        iter = Sequence.IterAt(segmentsList, index);
                    }
                }
                else
                {
                    iter = segments;
                }

                Path outPath = null;
                Segment? prev = null;
                var prevSelected = false;
                foreach (var segment in iter)
                {
                    var selected = segment.OnCurve.Selected == selValue;
                    if (selected)
                    {
                        if (prevSelected)
                        {
                            if (invert)
                            {
                                // XXX: bad
                                segment.Points.ForEach(point => point.Parent = outPath);
                            }
                            outPath._points.AddRange(segment.Points);
                        }
                        else
                        {
                            Point point;
                            outPath = new Path();
                            if (invert)
                            {
                                point = segment.OnCurve;
                                // XXX: bad. we're making assumptions about caller.
                                // here we are reparenting because we know original paths will be trashed
                                // (Outline.DeleteSelection) where !invert does a no copy work.
                                // I don't like this, and the delete apis returning a list of paths tbh....
                                outPath.Parent = path.Parent;
                                point.Parent = outPath;
                            }
                            else
                            {
                                point = segment.OnCurve.Clone();
                            }
                            point.Smooth = false;
                            point.Type = PointType.Move;
                            outPath._points.Add(point);
                        }
                    }
                    else
                    {
                        if (prevSelected)
                        {
                            if (invert)
                            {
                                prev.Value.OnCurve.Smooth = false;
                            }
                            // We got a path, export it and move on
                            outPaths.Add(outPath);
                            outPath = null;
                        }
                    }
                    prev = segment;
                    prevSelected = selected;
                }
                if (prevSelected)
                {
                    outPaths.Add(outPath);
                }
            }

            return outPaths;
        }
    }
}