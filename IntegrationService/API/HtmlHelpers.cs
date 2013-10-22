//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using IntegrationService.API.Models;
using Kanban.API.Client.Library.TransferObjects;
using ServiceStack.Html;

namespace IntegrationService.API
{
    public static class HtmlHelpers
    {
        public static string RenderBoardRepresentation(this HtmlHelper html, IEnumerable<LaneModel> laneStats,
                                               bool supportRollup, LaneDisplayType displayType)
        {
            return RenderBoardRepresentationImpl(laneStats, supportRollup, displayType);
        }

        public static string RenderBoardRepresentation(this HtmlHelper html, IEnumerable<LaneModel> laneStats,
                                                       bool supportRollup)
        {
            return RenderBoardRepresentationImpl(laneStats, supportRollup, LaneDisplayType.None);
        }

        public static string RenderBoardRepresentationStatic(IEnumerable<LaneModel> laneStats,
                                                       bool supportRollup)
        {
            return RenderBoardRepresentationImpl(laneStats, supportRollup, LaneDisplayType.None);
        }

        private static string RenderBoardRepresentationImpl(IEnumerable<LaneModel> laneStats, bool supportRollup,
                                                            LaneDisplayType displayType)
        {
            var sb = new StringBuilder();

            sb.Append("<table cellspacing='0' cellpadding='0' id='boardRepresentation'><tr>");
            RenderParentLanes(GetOrderedLanes(laneStats).Where(x => x.Level == 0), sb, supportRollup, displayType);
            sb.Append("</tr></table>");
            return sb.ToString();
        }

        private static IEnumerable<LaneModel> GetOrderedLanes(IEnumerable<LaneModel> laneStats)
        {
            var orderedLaneStats = new List<LaneModel>();

            Func<LaneModel, bool> backLogPred = x => x.ClassType == LaneClassType.Backlog && x.Relation != "child";
            Func<LaneModel, bool> activePred = x => x.ClassType == LaneClassType.Active;
            Func<LaneModel, bool> archivePred = x => x.ClassType == LaneClassType.Archive && x.Relation != "child";

            if (laneStats.Any(backLogPred))
            {
                orderedLaneStats.AddRange(laneStats.Where(backLogPred).OrderBy(x => x.Index));
            }

            if (laneStats.Any(activePred))
            {
                orderedLaneStats.AddRange(laneStats.Where(activePred).OrderBy(x => x.Index));
            }

            if (laneStats.Any(archivePred))
            {
                orderedLaneStats.AddRange(laneStats.Where(archivePred).OrderBy(x => x.Index));
            }

            return orderedLaneStats;
        }

        private static string GetRemovedTag(string title)
        {
            return title.Replace("<", "&lt;").Replace(">", "&gt;");
        }


        private static void RenderParentLanes(IEnumerable<LaneModel> statisticLane, StringBuilder sb, bool supportRollup, LaneDisplayType displayType)
        {
            var laneIndex = 1;
            foreach (var lane in statisticLane)
            {
                var parentLaneIds = new List<long>();

                if (lane.IsParent)
                {
                    sb.AppendFormat(
                        "<td><table cellspacing='0'><tr class='kb-ch-headerTr'><td colspan='{2}'><div id='{0}' class='kb-ch-laneHeaderRepresentation {4}' index='{5}'><div class='kb-ch-lane-name'><div id='rollup{0}' associatedLane='{0}' class='kb-ch-unrolled ui-icon ui-icon-circle-arrow-s' style='display:{3}' title='Rollup Child Lanes'></div>{1}</div></div></td></tr>",
                        lane.Id,
                        GetLaneDisplay(lane, displayType),
                        GetColumnSpan(lane),
                        supportRollup ? "block" : "none",
                        GetCssClassForLaneType(lane),
                        laneIndex);

                    parentLaneIds.Add(lane.Id);
                    RenderChildLanes(lane.ChildLanes, parentLaneIds, sb, supportRollup, displayType);
                }
                else
                {
                    sb.AppendFormat(
                        "<td><table cellspacing='0' class='kb-ch-childLaneTable'><tr class='kb-ch-headerTr'><td colspan='{2}'><div id='{0}' class='kb-ch-laneHeaderRepresentation {3}' index='{4}'><div class='kb-ch-lane-name'>{1}</div></td></tr><tr><td><div id='{0}' class='kb-ch-laneRepresentation'></div></td></tr>",
                        lane.Id,
                        GetLaneDisplay(lane, displayType),
                        GetColumnSpan(lane),
                        GetCssClassForLaneType(lane),
                        laneIndex);

                }

                sb.Append("</table></td>");
                laneIndex++;
            }
        }

        private static object GetCssClassForLaneType(LaneModel lane)
        {
            var cssClass = string.Empty;
            switch (lane.ClassType)
            {
                case LaneClassType.Active:
                    break;
                case LaneClassType.Backlog:
                    cssClass = "kb-ch-backlog";
                    break;
                case LaneClassType.Archive:
                    cssClass = "kb-ch-archive";
                    break;
            }
            return cssClass;
        }

        private static void RenderChildLanes(IList<LaneModel> statisticLane, ICollection<long> parentLaneIds,
                                             StringBuilder sb, bool supportRollup, LaneDisplayType displayType)
        {
            if (statisticLane[0].Orientation == Orientation.Vertical)
            {
                sb.Append("<tr>");
                foreach (var lane in statisticLane.OrderBy(x => x.Index))
                {
                    if (lane.IsParent)
                    {
                        sb.AppendFormat(
                            "<td colspan='{3}'><table cellspacing='0'><tr class='kb-ch-headerTr'><td colspan='{3}'><div id='{0}' class='kb-ch-laneHeaderRepresentation  {1} {5}'><div class='kb-ch-lane-name'><div id='rollup{0}' associatedLane='{0}' class='kb-ch-unrolled ui-icon ui-icon-circle-arrow-s' style='display:{4}' title='Rollup Child Lanes'></div>{2}</div></div></td></tr>",
                            lane.Id, string.Join(" ", parentLaneIds.Select(x => x.ToString()).ToArray()), GetLaneDisplay(lane, displayType),
                            GetColumnSpan(lane), supportRollup ? "block" : "none",
                            GetCssClassForLaneType(lane));
                        parentLaneIds.Add(lane.Id);
                        RenderChildLanes(lane.ChildLanes, parentLaneIds, sb, supportRollup, displayType);
                        parentLaneIds.Remove(lane.Id);
                    }
                    else
                    {
                        sb.AppendFormat(
                            "<td colspan='{3}'><table cellspacing='0' class='kb-ch-childLaneTable'><tr class='kb-ch-headerTr'><td><div id='{0}' class='kb-ch-laneHeaderRepresentation {1} kb-ch-laneRepresentation {4}'><div class='kb-ch-lane-name'>{2}</div></div></td></tr><tr><td><div id='{0}' class='kb-ch-laneRepresentation'></div></td></tr>",
                            lane.Id,
                            string.Join(" ", parentLaneIds.Select(x => x.ToString()).ToArray()),
                            GetLaneDisplay(lane, displayType),
                            GetColumnSpan(lane),
                            GetCssClassForLaneType(lane));
                    }
                    sb.Append("</table></td>");
                }
                sb.Append("</tr>");
            }
            else
                foreach (var lane in statisticLane.OrderBy(x => x.Index))
                {
                    sb.Append("<tr>");
                    if (lane.IsParent)
                    {
                        sb.AppendFormat(
                            "<td><table cellspacing='0'><tr class='kb-ch-headerTr'><td colspan='{3}'><div id='{0}' class='kb-ch-laneHeaderRepresentation {1} {5}'><div class='kb-ch-lane-name'><div id='rollup{0}' associatedLane='{0}' class='kb-ch-unrolled ui-icon ui-icon-circle-arrow-s' style='display:{4}' title='Rollup Child Lanes'></div>{2}</div></div></td></tr>",
                            lane.Id, string.Join(" ", parentLaneIds.Select(x => x.ToString()).ToArray()), GetLaneDisplay(lane, displayType),
                            GetColumnSpan(lane), supportRollup ? "block" : "none",
                            GetCssClassForLaneType(lane));
                        parentLaneIds.Add(lane.Id);
                        RenderChildLanes(lane.ChildLanes, parentLaneIds, sb, supportRollup, displayType);
                        parentLaneIds.Remove(lane.Id);
                    }
                    else
                    {
                        sb.AppendFormat(
                            "<td><table cellspacing='0' class='kb-ch-childLaneTable'><tr class='kb-ch-headerTr'><td colspan='{3}'><div id='{0}' class='kb-ch-laneHeaderRepresentation {1} kb-ch-laneRepresentation {4}'><div class='kb-ch-lane-name'>{2}</div></div></td></tr><tr><td><div id='{0}' class='kb-ch-laneRepresentation'></div></td></tr>",
                            lane.Id, string.Join(" ", parentLaneIds.Select(x => x.ToString()).ToArray()), GetLaneDisplay(lane, displayType),
                            GetColumnSpan(lane),
                            GetCssClassForLaneType(lane));
                    }
                    sb.Append("</table></td>");
                }
            sb.Append("</tr>");
        }

        private static string GetLaneDisplay(LaneModel lane, LaneDisplayType displayType)
        {
            var annotation = "";
            string activityName;

            //This is a fix for when the activities do not load correctly.
            try
            {
                activityName = lane.Activity == null ? "Undefined" : GetRemovedTag(lane.Activity.Name);
            }
            catch
            {
                activityName = "Undefined";
            }

            switch (displayType)
            {
                case LaneDisplayType.ActivityType:
                    annotation = string.Format("<br /><span class='kb-ch-lane-annotation'>({0})</span>", activityName);
                    break;
                case LaneDisplayType.LaneType:
                    annotation = string.Format("<br /><span class='kb-ch-lane-annotation'>({0})</span>", GetRemovedTag(lane.Type.ToString()));
                    break;
            }
            return string.Format("{0}{1}", GetRemovedTag(lane.Title), annotation);
        }

        private static int GetColumnSpan(LaneModel lane)
        {
            if (lane.ChildLanes == null || lane.ChildLanes.Count == 0 ||
                lane.ChildLanes.First().Orientation == Orientation.Horizontal)
                return 1;

            return lane.ChildLanes.Sum(l => GetColumnSpan(l));
        }

        public static string RenderCfdChartSettingsInput(this HtmlHelper html, string path, int xAxisMax)
        {
            var settings = new StreamReader(HttpContext.Current.Server.MapPath(path)).ReadToEnd();

            return string.Format("<div style=\"display:none;\" id=\"ChartSettingsPath\" >{0}</div>",
                                 settings.Replace("#xAxis", xAxisMax.ToString()).Replace('\"', '\''));
        }


    }
}