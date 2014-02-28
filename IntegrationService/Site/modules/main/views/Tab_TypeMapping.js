/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.models.TypeMapModel = Backbone.Model.extend();
    Main.models.TypeMapCollection = Backbone.Collection.extend({
        model:Main.models.TypeMapModel
    });
    

    Main.controllers.TypeMapTabController = NiceTools.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;
            this.leanKitTypes = options.leanKitTypes;
            this.targetTypes = options.targetTypes;
            this.allTargetTypes = options.targetTypes;

            this.applyfilters();

            this.model = new Backbone.Model();
            this.model.set("Target", App.request('getTargetType'));

            this.model.set("TypeCollection", options.collection);
                        
            // add blank item
            this.leanKitTypes.unshift("");
            this.targetTypes.unshift("");
            
            App.reqres.setHandler('leanKitTypes', function () {
                return this.leanKitTypes;
            }, this);
            
            App.reqres.setHandler('targetTypes', function () {
                return this.targetTypes;
            }, this);
            
            // subscribe to tab:activated events
            this.listenTo(Main, 'excludes:updated', this.excludesUpdated, this);
            this.listenTo(Main, 'tab:activated', this.tabActivated, this);
            this.listenTo(Main.boardConfiguration, 'projectTypesUpdated', this.updateProjectTypes, this);
            
            this.configureViews();
        },
        
        applyfilters: function() {
            if (_.isObject(this.options.owner.model.attributes.Excludes)) {
                var etypes = this.options.owner.model.attributes.Excludes.split(",");
                this.excludedTypes = _.map(etypes, function(exc) { return exc.trim(); });
                this.filterExcludes(this.excludedTypes);
            } else {
                this.excludedTypes = [];
            }            
        },

        updateProjectTypes: function (projectId, types) {
            if (this.owner.model.TargetProjectId() == projectId) {
                this.owner.model.TypeMap().reset();
                this.model.set("TypeCollection", this.owner.model.TypeMap());
                this.allTargetTypes = _.pluck(types, "Name");
                this.targetTypes = _.pluck(types, "Name");
                this.applyfilters();                
            }
        },

        autoMapMatchedItems:function (typeCollection) {
            // check for matches in leanKitTypes and targetTypes.
            // if found, and not already in options.collection, add it to the collection
            
            var l1 = this.leanKitTypes.length;
            var l2 = this.targetTypes.length;
            var biggerList, smallerList, type, foundType;
            biggerList = l1 > l2 ? this.leanKitTypes : this.targetTypes;
            smallerList = l1 <= l2 ? this.leanKitTypes : this.targetTypes;
            var len = Math.max(l1, l2);
            for (var i = 0; i < len; i++) {
                type = biggerList[i];
                if (type === "") continue;
                if (smallerList.indexOf(type) >= 0) {
                    // is it in typeCollection already
                    // no? then add it
                    foundType = typeCollection.findWhere({ TargetType: type });
                    if (_.isUndefined(foundType)) {
                        var m = new Main.models.TypeMapModel({ LeanKitType: type, TargetType: type });
                        typeCollection.add(m);
                    }
                }
            }

        },
        configureViews: function () {
            this.view = new Main.views.TypeMapTabView({ controller: this, model: this.model });
            
            this.viewFactory = new App.ViewFactory(this, "TypeMapTab");
            
            this.viewFactory.register("typeMapCollection", function (c) {
                var view = new Main.views.TypeMapCollectionView({ collection: c.model.get("TypeCollection"), controller:c });
                
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

        filterExcludes: function(excludes) {
            this.targetTypes = _.reject(this.allTargetTypes, function (t) { return excludes.indexOf(t) > -1; });
            this.targetTypes.unshift("");
        },

        excludesUpdated: function (excludes) {
            this.filterExcludes(excludes);
            this.excludedTypes = excludes;
        },
        
        tabActivated: function (tabName) {
            if (tabName !== "Card Types") return;
            this.autoMapMatchedItems(this.model.get("TypeCollection"));
        },
        
        onClose:function () {
            this.stopListening();
        }

    });

    Main.views.TypeMapItemView = NiceTools.BoundView.extend({
       template:this.template("typeMapItem"),
       events: {
           "click #remove": "removeRequested",
           "click #confirm": "confirmed",
       },
       
        ui: {
            "removeBtn":"#remove",
            "confirmBtn":"#confirm"
        },
        initialize:function () {
            this.initializeBindings();
        },
            
       onShow:function () {
           this.bindModel();
           this.delegateEvents();
       },
       
       onItemSelected: function () {
           if (this.M("LeanKitType") !== "" && this.M("TargetType") !== "")
               this.ui.confirmBtn.removeClass("disabled");
       },
                     
        removeRequested:function (e) {
            this.model.collection.remove(this.model);
        },
       
        confirmed:function () {
            this.render();
            this.delegateEvents();
            this.model.set("IsConfirmed", true);
        }
       
       
    });

    Main.views.TypeMapCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.TypeMapItemView
    });
    
    Main.views.TypeMapTabView = Marionette.Layout.extend({
        template: this.template("tab_typeMapping"),

        events: {
            "click #add": "addRequested"
        },
        
        initialize: function (options) {
            this.controller = options.controller;
            App.reqres.setHandler("checkIf_isNew", function (model) {
                return model.LeanKitType === "" && model.TargetType === "";
            }, this);
        },

        onShow: function () {
            if (this.controller) this.controller.triggerMethod("prep:nestedViews");
        },
        
        onClose: function () {
            this.controller.triggerMethod("close");
        },

        addRequested: function () {
            this.model.get("TypeCollection").add(new Main.models.TypeMapModel({ LeanKitType: "", TargetType: "" }));
        },
        
    });


});


