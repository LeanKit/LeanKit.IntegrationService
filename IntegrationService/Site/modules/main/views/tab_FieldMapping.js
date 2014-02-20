/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    //Main.models.FieldMapModel = Backbone.Model.extend();
    //Main.models.FieldMapCollection = Backbone.Collection.extend({
    //    model:Main.models.FieldMapModel
    //});
    
    Main.controllers.FieldMapTabController = NiceTools.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;
            this.configuration = options.configuration;

            this.model = new Backbone.Model();

            this.model.set("Target", App.request('getTargetType'));
            this.model.set("FieldCollection", options.configuration);
                                            
            // subscribe to tab:activated events
            this.listenTo(Main, 'tab:activated', this.tabActivated, this);
            
            this.configureViews();
        },

        configureViews: function () {
            this.view = new Main.views.FieldMapTabView({ controller: this, model: this.model });
            
            this.viewFactory = new App.ViewFactory(this, "FieldMapTab");
            
            this.viewFactory.register("fieldMapCollection", function (c) {
                var view = new Main.views.FieldMapCollectionView({ collection: c.model.get("FieldCollection"), controller:c });                
                return view;
            });            
        },
        
        onPrepNestedViews: function () {
            this.viewFactory.each(function (nestedView) {
                if (_.isString(nestedView.id)) {
                    var region = this.view[nestedView.id];
                    region.show(nestedView);
                }
            }, this);
        },
        
        tabActivated: function (tabName) {
            if (tabName !== "Fields") return;           
        },
        
        onClose:function () {
            this.stopListening();
        }

    });

    Main.views.FieldMapItemView = NiceTools.BoundView.extend({
       template:this.template("fieldMapItem"),
       initialize:function () {
            this.initializeBindings();
        },           
       onShow:function () {
           this.bindModel();
           this.delegateEvents();
       },
    });

    Main.views.FieldMapCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.FieldMapItemView,
    });
    
    Main.views.FieldMapTabView = Marionette.Layout.extend({
        template: this.template("tab_fieldMapping"),

        events: {
            "change #TargetField": "updateSelectedTargetField",
            "change #SyncDirection": "updateSelectedSyncDirection"
        },

        initialize: function (options) {
            this.controller = options.controller;
            App.reqres.setHandler('getTargetFields', function (model) {
                var attrs = _.pluck(model.TargetFields.models, "attributes");
                return _.pluck(attrs, "Name");
            }, this);
            App.reqres.setHandler('getSyncDirections', function (model) {
                return model.SyncDirections;
            }, this);
            App.reqres.setHandler('isTargetFieldSelected', function (model, idx) {
                if (model.TargetFields.models[idx].IsSelected())
                    return true;
                return false;
            }, this);
            App.reqres.setHandler('isSyncDirectionSelected', function (model, idx) {
                if (model.SyncDirections[idx] == model.SyncDirection)
                    return true;
                return false;
            }, this);
        },

        onShow: function () {
            if (this.controller) this.controller.triggerMethod("prep:nestedViews");
        },
        
        onClose: function () {
            this.controller.triggerMethod("close");
        },
        
        updateSelectedTargetField: function (e) {
            var item = this.model.get("FieldCollection").findWhere({ LeanKitField: e.target.getAttribute("data-leankitfield") });
            if (!_.isUndefined(item)) {
                var targetFields = item.get("TargetFields");
                for (var i = 0; i < targetFields.length; i++) {
                    targetFields.at(i).IsSelected(false);
                    if (targetFields.at(i).Name() == e.target.value) {
                        targetFields.at(i).IsSelected(true);
                    }
                }                
            }
        },
        
        updateSelectedSyncDirection: function (e) {
            var item = this.model.get("FieldCollection").findWhere({ LeanKitField: e.target.getAttribute("data-leankitfield") });
            if (!_.isUndefined(item)) {
                item.SyncDirection(e.target.value);
            }
        },
    });
});


