
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LeanKit.API.Client.Library.TransferObjects;

namespace IntegrationService
{
	public class BoardMapping
	{

		public BoardMapping()
		{
			Types=new List<WorkItemType>();
			FieldMappings = new List<FieldMap>();
			Filters = new List<Filter>();
			LaneToStatesMap = new Dictionary<long, List<string>>();
			QueryDaysOut = 7;
		}

		// populated by user, via config file
		public Identity Identity { get; set; }
		public List<string> QueryStates { get; set; }
		public Dictionary<long, List<string>> LaneToStatesMap { get; set; }
		public List<FieldMap> FieldMappings { get; set; } 
		public List<WorkItemType> Types { get; set; }
		public List<Filter> Filters { get; set; } 
		public string Excludes { get; set; }
		public string Query { get; set; }
		public int QueryDaysOut { get; set; }
		public string IterationPath { get; set; }
		public bool CreateCards { get; set; }
		public bool UpdateCards { get; set; }
		public bool UpdateCardLanes { get; set; }
		public bool UpdateTargetItems { get; set; }
		public bool CreateTargetItems { get; set; }
		public bool TagCardsWithTargetSystemName { get; set; }
		public long DefaultCardCreationLaneId { get; set; }

		// populated by app
		public string ExcludedTypeQuery { get; set; }
		public IList<Lane> ValidLanes { get; set; } 
		public IList<CardType> ValidCardTypes { get; set; }
		public long ArchiveLaneId { get; set; }

		public List<long> LanesFromState(string state)
		{
			var laneIds = (from lane in LaneToStatesMap 
					where lane.Value.Any(val => val.Equals(state, StringComparison.OrdinalIgnoreCase)) 
					select lane.Key).ToList();
			if (laneIds.Any())
				return laneIds;

			laneIds = (from lane in ValidLanes
			           where lane.Name.Equals(state, StringComparison.OrdinalIgnoreCase)
			           select lane.Id).ToList();
			if (laneIds.Any())
				return laneIds;	

			return ValidLanes.Select(x => x.Id).ToList();
		}

		public List<string> GetTargetFieldFor(LeanKitField leanKitField, SyncDirection syncDirection)
		{
			var targets = new List<string>();

			if (FieldMappings.Any())
			{
				FieldMap field = null;

				switch (syncDirection)
				{
					case SyncDirection.ToLeanKit:
						field = FieldMappings.FirstOrDefault(x => (x.LeanKitField.ToLowerInvariant() == leanKitField.ToString().ToLowerInvariant())
						                                          && ((x.SyncDirection.ToLowerInvariant() == "toleankit") || (x.SyncDirection.ToLowerInvariant() == "both")));
						break;
					case SyncDirection.ToTarget:
						field = FieldMappings.FirstOrDefault(x => (x.LeanKitField.ToLowerInvariant() == leanKitField.ToString().ToLowerInvariant())
						                                          && ((x.SyncDirection.ToLowerInvariant() == "totarget") || (x.SyncDirection.ToLowerInvariant() == "both")));
						break;
					default:
						field = FieldMappings.FirstOrDefault(x => (x.LeanKitField.ToLowerInvariant() == leanKitField.ToString().ToLowerInvariant())
						                                          && (x.SyncDirection.ToLowerInvariant() == syncDirection.ToString().ToLowerInvariant()));
						break;
				}

				if (field != null)
				{
					var selectedFields = field.TargetFields.Where(x => x.IsSelected).Select(x => x.Name);
					if (selectedFields.Any())
						return selectedFields.ToList();

					var defaultFields = field.TargetFields.Where(x => x.IsDefault).Select(x => x.Name);
					if (defaultFields.Any())
						return defaultFields.ToList();
				}
			}

			return targets;
		}

		public List<long> LanesFromState(string state, bool creationEvent)
		{
			// identify all valid lanes for card creation
			var validNonChildLaneIds = ValidLanes.Where(x => x.HasChildLanes.Equals(false))
				.Select(x => x.Id).ToArray();

			// identify all mapped lanes for this state
			var mappedLaneIds = (from lane in LaneToStatesMap
				where lane.Value.Any(val => val.Equals(state, StringComparison.OrdinalIgnoreCase))
				select lane.Key).ToArray();

			// identify all valid mapped lanes
			var validMappedLaneIds = (validNonChildLaneIds.Intersect(mappedLaneIds)).ToList();

			// in case no valid mapped lanes exist but the DefaultCardCreationLane is specified, add it
			// otherwise, add first valid non-mapped lane
			if (!validMappedLaneIds.Any())
			{
				validMappedLaneIds = (DefaultCardCreationLaneId > 0)
					? new List<long> {DefaultCardCreationLaneId}
					: new List<long> {validNonChildLaneIds.First()};
			}

			return validMappedLaneIds;
		}

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.Append("     Identity :       " + Environment.NewLine + Identity);           
			if(LaneToStatesMap.Any())
			{
				sb.Append(Environment.NewLine);
				sb.Append("      Lane to States:   " + Environment.NewLine);
				foreach (var item in LaneToStatesMap)
				{
					sb.Append("        " + item.Key + ": ");
					foreach(var state in item.Value)
						sb.Append(state + ", ");
					sb.Append(Environment.NewLine);
				}
			}

			sb.Append(Environment.NewLine);
			sb.Append("     Field Mappings   :        " + Environment.NewLine);
			foreach (var fieldMapping in FieldMappings) {
				sb.Append(fieldMapping);
			}

			sb.Append(Environment.NewLine);
			sb.Append("     Types :          " + Environment.NewLine);
			foreach (var workItemType in Types)
			{
				sb.AppendLine("          WorkItemType : " + workItemType);
			}
			sb.Append("     Excludes:        " + Excludes + Environment.NewLine);

			if (ValidLanes != null)
			{
				sb.Append(Environment.NewLine);
				sb.Append("     ValidLanes :     ");
				foreach (var validLane in ValidLanes)
				{
					sb.Append(validLane.Id + ", ");
				}
			}

			if (ValidCardTypes != null)
			{
				sb.Append(Environment.NewLine);
				sb.Append("     ValidCardTypes : ");
				foreach (var validCardType in ValidCardTypes)
				{
					sb.Append(validCardType.Name + ", ");
				}
			}

			sb.Append(Environment.NewLine);
			sb.AppendLine("     Query :                         " + Query);
			sb.AppendLine("     QueryDaysOut:                   " + QueryDaysOut.ToString());
			sb.AppendLine("     ArchiveLaneId :                 " + ArchiveLaneId);
			sb.AppendLine("     TagCardsWithTargetSystemName :  " + TagCardsWithTargetSystemName);
			sb.AppendLine("     CreateCards :                   " + CreateCards);
			sb.AppendLine("     CreateTargetItems :             " + CreateTargetItems);
			sb.AppendLine("     UpdateCards :                   " + UpdateCards);
			sb.AppendLine("     UpdateCardLanes :               " + UpdateCardLanes);
			sb.AppendLine("     UpdateTargetItems :             " + UpdateTargetItems);
            sb.AppendLine("     DefaultCardCreationLaneId :             " + DefaultCardCreationLaneId);
            return sb.ToString();
		}
	}

	public class FieldMap
	{
		public FieldMap() 
		{
			TargetFields = new List<TargetFieldMap>();
		}

		public string LeanKitField { get; set; }
		public string SyncDirection { get; set; }
		public List<TargetFieldMap> TargetFields { get; set; }

		public override string ToString() 
		{
			var sb = new StringBuilder();
			sb.AppendLine("     LeanKitField :       " + LeanKitField);
			sb.AppendLine("     SyncDirection:       " + SyncDirection);
			if (TargetFields.Any()) {
				sb.AppendLine("      TargetFields:   ");
				foreach (var item in TargetFields) {
					sb.AppendLine("        " + item.Name + ", IsDefault: " + item.IsDefault + ", IsSelected: " + item.IsSelected);
				}
			}
			return sb.ToString();
		}
	}

	public class TargetFieldMap 
	{
		public string Name { get; set; }
		public bool IsDefault { get; set; }
		public bool IsSelected { get; set; }
	}

	public enum FilterType
	{
		Include, 
		Exclude
	}

	public class Filter
	{
		public string TargetFieldName { get; set; }
		public FilterType FilterType { get; set; }
		public string FilterValue { get; set; }
	}
}