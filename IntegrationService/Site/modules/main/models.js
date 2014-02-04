/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.models.ServerConfiguration = App.codegen.ServerConfigurationModel.extend({
        isValid: function() {
            var h = this.get("Host");
            return (_.isString(h) && h !== "");
        },
        asQueryString:function (strToAppend) {
            return $.param(this.attributes) + strToAppend;
        },
        defaults: {
            Protocol:"https://"
        }
    });

    Main.models.LaneStateMapCollection = Backbone.Collection.extend({
        model: App.codegen.LaneStateModel
    });

    Main.models.TypeMapModel = App.codegen.TypeMapModel.extend({
        defaults: {
            LeanKitType: "",
            TargetType: "",
            IsConfirmed:false
        },
    });
    
    Main.models.TypeMapCollection = Backbone.Collection.extend({
        model: Main.models.TypeMapModel,
        isNew2:function () {
            return this.attributes.LeanKitType != "" && this.attributes.TargetType != "";
        }
    });
    
    Main.models.BoardMapping = App.codegen.BoardMappingModel.extend({
        urlRoot: "/mapping",
        idAttribute:"BoardId",
        defaults: {
            Id: null,
            BoardId: null,
            LaneToStatesMap: undefined,
            TypeMap: undefined,
            CreateCards: true,
            CreateTargetItems: false,
            UpdateCards: true,
            UpdateTargetItems: false,
            TagCardsWithTargetSystemName: false
        },
        parse: function (item) {
            if (item === "OK") return; // side effect of saving; ignore
            
            this.BoardId(item.BoardId);
            this.TargetProjectId(item.TargetProjectId);
            this.UpdateCards(item.UpdateCards);
            this.UpdateTargetItems(item.UpdateTargetItems);
            this.TypeMap(new Main.models.TypeMapCollection(item.TypeMap));
            this.isDirty = false;
        },
        
        initialize: function () {
            // init LaneToStateMap & TypeMap
            if (_.isUndefined(this.attributes.LaneToStatesMap))
                this.attributes.LaneToStatesMap = { };

            if (_.isUndefined(this.attributes.TypeMap))
                this.attributes.TypeMap = { };

            // convert TypeMap to collection
            var tm = this.TypeMap();
            var typeCollection;
            if (_.isUndefined(tm) || _.isUndefined(tm.length) || tm.length === 0)
                typeCollection = new Main.models.TypeMapCollection();
            else
                typeCollection = new Main.models.TypeMapCollection(tm);

            // overwrite typeMap with backbone collection
            this.TypeMap(typeCollection);
            
            this.listenTo(typeCollection, "all", this.onTypeMapUpdate, this);
            this.listenTo(this, "change", this.onChange, this);
            this.isDirty = false;
        },
        
        onChange:function (m) {
            this.isDirty = true;
        },
        
        onTypeMapUpdate: function (e, m) {
            if (e === "remove" || e === "change:IsConfirmed" ) {
                this.changed["TypeMap"] = this.TypeMap();
                this.trigger('change', this);
            }
        },
                
        hasProject: function () {
            var tpid = this.TargetProjectId();
            return _.isString(tpid) && tpid !== "";
        },
        
        addStateToLane: function (laneId, state) {
            var laneStates = this.LaneToStatesMap()[laneId];
            if (_.isUndefined(laneStates)) {
                laneStates = [];
                this.LaneToStatesMap()[laneId] = laneStates;
            }
            if (laneStates.indexOf(state) === -1) {
                laneStates.push(state);
                this.changed["LaneToStatesMap"] = this.LaneToStatesMap();
            }
            this.trigger('change', this);
        },
        
        removeStateFromLane:function(laneId, state) {
            var laneStates = this.LaneToStatesMap()[laneId];
            if (_.isUndefined(laneStates)) return;
            var pos = laneStates.indexOf(state);
            if (pos >= 0) {
                laneStates.splice(pos, 1);
                this.changed["LaneToStatesMap"] = this.LaneToStatesMap();
            }
            this.trigger('change', this);
        },
          
        isValid: function () {
            // has project
            if (!this.hasProject()) return false;

            // has at least 1 QueryState OR a Query defined
            var queryStates = this.QueryStates();
            if (_.isUndefined(queryStates)) return false;
            var queryStatesOk = queryStates.length > 0;

            var query = this.Query();
            var queryOk = _.isString(query) && query !== "";

            if (queryOk) return true; // don't need to validate queryStates if the query is ok

            // If QueryStates used, each state in QueryState is associated with a lane
            this.unassignedStates = queryStates.slice(0);

            if (!queryStatesOk) return false;

            var lsm = this.LaneToStatesMap();
            var lanes = _.keys(lsm);
            var state, lane, idx;
            for (var i = 0; i < queryStates.length; i++) {
                state = queryStates[i].toLowerCase();
                for (var j = 0; j < lanes.length; j++) {
                    lane = lanes[j];
                    if (lsm[lane].map(function (elem) { return elem.toLowerCase(); }).indexOf(state) >= 0) {
                        // found, remove from unassignedStates
                        idx = this.unassignedStates.map(function (elem) { return elem.toLowerCase(); }).indexOf(state);
                        if (idx >= 0) {
                            this.unassignedStates.splice(idx, 1);
                        }
                    }
                }
            }

            return (this.unassignedStates.length === 0);
        }        
    });

    Main.models.MappingCollection = Backbone.Collection.extend({
        model: Main.models.BoardMapping,
        url:"/mappings",
        isValid: function () {
            return false;
        }
    });

    Main.models.ConfigurationSummary = NiceTools.Model.extend({
        url: "/configuration"
    });

    Main.models.ConfigurationSettingsModel = App.codegen.ConfigurationSettingsModel.extend({
        url: "/settings",
        defaults: {
            PollingFrequency: 5000,
            EarliestSyncDate: "1/1/2007"
        }
    });
    
    Main.models.Configuration = App.codegen.ConfigurationModel.extend({
        url: "/configuration",
        parse: function (item) {
            this.Target(new Main.models.ServerConfiguration(item.Target));
            this.LeanKit(new Main.models.ServerConfiguration(item.LeanKit));
            this.Mappings(new Main.models.MappingCollection(item.Mappings));
            this.Settings(new Main.models.ConfigurationSettingsModel(item.Settings));
        }
    });
    

    Main.models.BoardCollection = Backbone.Collection.extend({
        model: App.codegen.BoardListItem,
        url: "/boards",
        comparator: function (board) {
            var tpid = board.TargetProjectId();
            if (_.isString(tpid) && tpid !== "")
                return "0000_" + board.get("Title");
            else
                return board.get("Title");
        }
    });

    Main.models.BoardLaneNamesCollection = Backbone.Collection.extend({
        url: "/lanenames"
    });

    Main.models.ProjectCollection = Backbone.Collection.extend({
        model: App.codegen.ProjectListItem,
        url: "/projects"
    });

    // board detail model
    Main.models.LaneCollection = Backbone.Collection.extend({ model: App.codegen.LaneModel });
    
    Main.models.CardTypeCollection = Backbone.Collection.extend({ model: App.codegen.CardTypeModel });
   
    Main.models.Board = App.codegen.Board.extend({
        url: "/board",
        parse: function (item) {
            this.Id(item.Id);
            this.Title(item.Title);
            this.Lanes(new Main.models.LaneCollection(item.Lanes));
            this.CardTypes(new Main.models.CardTypeCollection(item.CardTypes));
            this.LaneHtml(item.LaneHtml);
        }
    });

    Main.models.syncRule = { leankit: 0, target: 1, both: 2 };
});
