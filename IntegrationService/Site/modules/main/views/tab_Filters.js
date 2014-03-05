/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {
  
    Main.controllers.FiltersTabController = NiceTools.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;
            this.configuration = options.configuration;
            this.filterfields = options.filterfields;
            this.model = new Backbone.Model();

            this.model.set("Target", App.request('getTargetType'));
            this.model.set("FilterCollection", options.configuration);
                                            
            // subscribe to tab:activated events
            this.listenTo(Main, 'tab:activated', this.tabActivated, this);
            
            this.configureViews();
        },

        configureViews: function () {
            this.view = new Main.views.FiltersTabView({ controller: this, model: this.model });
            
            this.viewFactory = new App.ViewFactory(this, "FiltersTab");
            
            this.viewFactory.register("filtersCollection", function (c) {
                var view = new Main.views.FilterCollectionView({ collection: c.model.get("FilterCollection"), controller:c });                
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
            if (tabName !== "Filters") return;           
        },
        
        onClose:function () {
            this.stopListening();
        }

    });

    Main.views.FilterItemView = NiceTools.BoundView.extend({
       template:this.template("filterItem"),

       events: {
           "click #remove": "removeRequested",
       },

       ui: {
           "removeBtn": "#remove",
       },
       
       initialize: function () {
           this.initializeBindings();
       },

       onShow: function () {
           this.bindModel();
           this.delegateEvents();
       },

       removeRequested: function (e) {
           this.model.collection.remove(this.model);
       },
    });

    Main.views.FilterCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.FilterItemView,
    });
    
    Main.views.FiltersTabView = Marionette.Layout.extend({
        template: this.template("tab_filters"),

        events: {
            "click #confirm": "confirmed",
        },

        ui: {
            "confirmBtn": "#confirm",
            "filterType": "#FilterType",
            "targetField": "#TargetField",
            "filterValue": "#FilterValue"
        },

        initialize: function (options) {
            this.controller = options.controller;
            App.reqres.setHandler('getFilterFields', function (model) {                
                return this.controller.filterfields;
            }, this);
            App.reqres.setHandler('getFilterTypes', function (model) {
                return ["Exclude", "Include"];
            }, this);
        },
        
        onShow: function () {
            if (this.controller) this.controller.triggerMethod("prep:nestedViews");
        },
        
        onClose: function () {
            this.controller.triggerMethod("close");
        },
        
        confirmed: function () {            
            this.model.get("FilterCollection").add(
                new Main.models.FilterModel({
                    FilterType: this.ui.filterType.val(),
                    FilterValue: this.ui.filterValue.val(),
                    TargetFieldName: this.ui.targetField.val()
                }));
        }
    });
});


