/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.ActivateController = NiceTools.Controller.extend({
        initialize: function(options) {
            this.owner = options.owner;
            this.pageName = options.pageName;
            this.model = options.model; //.collapse();

            App.reqres.setHandler("getMappingCount", this.getMappingCount, this);
            
        },

        isValid: function () {
            return false; // can't validate this page
        },

        onPageShow: function () {
            this.configureViews();
        },

        onPageLeave: function () {
            this.view.close();
            this.view = null;
            this.viewFactory.close();
            this.viewFactory = null;
        },

        configureViews: function() {
            this.view = new Main.views.ActivateView({ model: this.model, controller: this });
            this.viewFactory = new App.ViewFactory(this, "activate");

            this.viewFactory.register("mappingSummaryView", function (c) {
                var view = new Main.views.MappingSummaryView({ collection: c.model.Mappings() });
                return view;
            });

        },

        getMappingCount: function(options, model) {
            return this.model.Mappings().length;
        },
        
        activateNow: function () {
            App.log("Activate clicked");
            var x = new Date().getUTCMilliseconds().toString();
            $.ajax({
                url: '/activate?x=' + x,
                type: 'PUT',
                contentType: "application/json",
                success: function(a, b, c) {
                    this.view.activatedOk();
                    App.log("Activated");
                },
                error: function(xhr, err, msg) {
                    this.view.activateFailed(msg);
                },
                context: this
            });
        }

    });
    

    Main.views.ActivateView = Main.views.PageView.extend({
        template: this.template("page_activate"),
        className:"panel panel-primary",
        ui: {
            "message":"#message",
            "activateBtn":"#activate-now"
        },
        
        events:{"click #activate-now":"activateNow"},

        initialize: function(options) {
            this.controller = options.controller;
            this.targetType= App.request("getTargetType");

            App.reqres.setHandler("getLaneToStatesMap", this.getLaneToStatesMap, this);
            App.reqres.setHandler("getCardTypeMapping", this.getCardTypeMapping, this);
            App.reqres.setHandler("getFieldMapping", this.getFieldMapping, this);
            App.reqres.setHandler("getCachedTargetType", this.getCachedTargetType, this);
            App.reqres.setHandler("checkIf_targetIsTfs", this.checkIf_targetIsTfs, this);
            App.reqres.setHandler("checkIf_useCustomQuery", this.checkIf_useCustomQuery, this);
        },


        onShow: function () {
            this.delegateEvents();
            this.controller.triggerMethod('prep:nestedViews');
        },
        

        getLaneToStatesMap: function (options, model) {
            var html = "<div>";
            var boardLanes = App.Config.laneNames[model.BoardId];
            
            _.each(boardLanes, function(lane) {
                if (_.isObject(model.LaneToStatesMap[lane.Id])) {
                    html += String.format("<div><strong>{0}:&nbsp;</strong>{1}</div>",
                        lane.Name,
                        model.LaneToStatesMap[lane.Id].toString().replace(/,/g, ", "));
                }
            }, this);
            
            html += "</div>";
            return html;
        },
        
        getCardTypeMapping: function (options, model) {
            var html = "";
            for (var i = 0; i < model.TypeMap.models.length; i++) {
                var map = model.TypeMap.models[i];
                html += String.format("<div><strong>{0}:&nbsp;</strong>{1}</div>",
                    map.get("LeanKitType"), map.get("TargetType"));
            }
            return html;
        },

        getFieldMapping: function (options, model)
        {
            var html = "";            
            for (var i = 0; i < model.FieldMap.models.length; i++)
            {
                var map = model.FieldMap.models[i];
                if (_.isObject(map) && !_.isUndefined(map)) {
                    html += String.format("<div>LeanKit Field: <strong>{0}</strong><div>Sync Direction: <strong>{1}</strong></div><div>Target Fields:{2}</div><br /></div>",
                        map.get("LeanKitField"), map.get("SyncDirection"), this.getTargetFieldMapping(map.get("TargetFields")));
                }
            }
            return html;
        },
        
        getTargetFieldMapping: function(targetFields) {
            var html = "";
            for (var j = 0; j < targetFields.length; j++) {
                var map = targetFields[j];
                if (_.isObject(map) && !_.isUndefined(map)) {
                    if (map.IsDefault || map.IsSelected) {
                        html += String.format("<div>&nbsp;&nbsp;<strong>{0}</strong>, IsDefault: {1}, IsSelected: {2}</div>", map.Name, map.IsDefault, map.IsSelected);
                    }
                }
            }
            return html;
        },
        
        getCachedTargetType: function () {
            return this.targetType;
        },
        
        checkIf_targetIsTfs: function (model) {
            return this.targetType === 'TFS';
        },
        
        checkIf_useCustomQuery:function (model) {
            return (_.isString(model.Query) && model.Query > "");
        },
        
        activateNow: function () {
            this.ui.activateBtn.addClass("disabled");
            this.ui.message.html("Restarting Service...");
            this.controller.activateNow();
        },
        
        activatedOk: function () {
            this.ui.message.html("Activation Complete!");
            this.ui.message.addClass("text-success");
            this.ui.activateBtn.removeClass("disabled");
        },
        
        activateFailed:function (msg) {
            this.ui.message.html("Activation Failed:" + msg);
            this.ui.message.addClass("text-danger");
            this.ui.activateBtn.removeClass("disabled");
        }
        
        
    });

    Main.views.MappingSummaryItemView = Marionette.ItemView.extend({
        template:this.template("mappingSummaryItem"),
        className: "col-xs-6",
        templateHelpers: {
            "QueryStatesSpaced": function () {
                return this.QueryStates.toString().replace(/,/g, ", ");
            }
        }
    });
    _

    Main.views.MappingSummaryView = Marionette.CollectionView.extend({
        itemView: Main.views.MappingSummaryItemView,
    });

});
