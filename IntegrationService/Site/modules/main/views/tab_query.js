/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.QueryTabController = Marionette.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;
            this.model = options.model;
            this.targetTypes = options.targetTypes;
            this.configureViews();
            this.typesView = undefined;
            this.listenTo(Main.boardConfiguration, 'projectTypesUpdated', this.updateProjectTypes, this);
        },
                
        configureViews:function () {
            this.view = new Main.views.QueryTabView({ controller: this, model: this.model });

            this.viewFactory = new App.ViewFactory(this, "tab_query");
            
            this.viewFactory.register("States", function (c) {
                var states = App.request("getStates");
                c.stateCollection = new Main.models.CheckboxCollection(states);
                var view = new Main.views.CheckboxCollectionView({ id: "States", collection: c.stateCollection });

                return view;
            });           

            this.viewFactory.register("Types", function (c) {
                c.typeCollection = new Main.models.CheckboxCollection(c.targetTypes);
                c.typeCollection.each(function(type) {
                    type.set("Checked", true);
                }, c);
                c.typesView = new Main.views.CheckboxCollectionView({ id: "Types", collection: c.typeCollection });

                return c.typesView;
            });

            this.viewFactory.register("Filters", function (c) {
                var paths = App.request("getFilterPaths");
                return new Main.views.SelectView({ id: "PathPicker", collection: paths});
            });
        },
        
        updateProjectTypes: function (projectId, types) {
            if (this.model.TargetProjectId() == projectId) {
                this.targetTypes = types;
                this.model.Excludes(null);           
                this.typesView.collection.reset();
                this.typeCollection = new Main.models.CheckboxCollection(this.targetTypes);
                this.typeCollection.each(function (type) {
                    type.set("Checked", true);
                    this.typesView.collection.add(type);
                }, this);
                this.viewFactory.register("Types", function(c) {
                    return c.typesView;
                });
                this.trigger("typesViewUpdated", this);
            }
        },
        
    });

    Main.views.QueryTabView = NiceTools.ItemView.extend({
        template: this.template("tab_query"),
        initialize: function(options) {
            this.controller = options.controller;
            App.reqres.setHandler("pathFilters", this.selectFilters, this);
            this.listenTo(this.controller, "typesViewUpdated", this.reRenderTypesView, this);
        },

        events: {
            "change label.checkbox": "checkboxChanged",
            "change div#query textarea": "queryChanged",
            "click #radio-simple": "simpleModeSelected",
            "click #radio-custom": "customModeSelected",
            "change #iteration-paths": "changePath",
            "change #QueryDaysOut": "queryDaysOutChanged"
        },

        ui: {
            "simple": "#simple",
            "custom": "#custom",
            "iterationPath": "#iteration-paths",
            "queryDaysOutContainer": "#QueryDaysOutContainer"
        },

        reRenderTypesView: function () {
            var html = "";
            var view = this.controller.viewFactory.createView("Types");
            if (_.isObject(view)) {
                var el = view.render().el;
                html = el.innerHTML;
                view.close();
                this.controller.viewFactory.remove(view);
            }
            this.$("#Types").html(html);
        },

        onShow: function () {

            if (App.request("targetAllowsIterationPath")) {
                // set iteration Path
                var path = this.model.IterationPath();
                if (_.isUndefined(path)) {
                    // new project; set default value
                    path = this.model.TargetProjectName();
                    this.model.IterationPath(path);
                }
                    this.ui.iterationPath.find("option").filter(function () {
                    return this.text === path;
                }).prop("selected", true);
            } else {
                this.$("#iteration-path-section").addClass("hide");
            }
            
            if (App.request("getTargetType") == "MicrosoftProject") {
                this.ui.queryDaysOutContainer.show();
            } else {
                this.ui.queryDaysOutContainer.hide();
            }
            
            // set states
            var statesArr = this.model.QueryStates();
            if (_.isObject(statesArr) && statesArr.length != 0) {
                _.each(statesArr, function (state) {
                    if (state !== "") {
                        var id = state.trim().toId();
                        var cb = this.$("div#States label" + id + " input");
                        if (_.isObject(cb))
                            cb[0].checked = true;
                    }
                });
            }

            // set excluded types
            var excludes = this.model.Excludes();
            if (_.isString(excludes) && excludes !== "") {
                var excludesArr = excludes.split(',');
                if (excludesArr.length === 0) return;
                _.each(excludesArr, function(excludedType) {
                    var id = excludedType.trim().toId();
                    var cb = this.$("div#Types label" + id + " input");
                    if (_.isObject(cb) && cb.length > 0)
                        cb[0].checked = false;
                }, this);

            }

            // set advanced query
            var qry = this.model.Query();
            this.$("div#query textarea").val(qry);
            if(_.isString(qry)&&qry !== "") {
                this.customModeSelected();
                this.$("#radio-custom").prop('checked', true);
            }

        },
        
        checkboxChanged: function (e) {
            var root = $(e.currentTarget).closest("div");
            var id = root[0].id;
           // var el = $(e.currentTarget).find("input")[0];
            var cbs = root.find("input");

            if (id === "States") {
                var checkedCbs = cbs.filter(function(index, cb) {
                    return cb.checked;
                }, this);

                var queryStates = [];
                _.each(checkedCbs, function(cb) {
                    queryStates.push(cb.value);
                }, this);

                this.model.QueryStates(queryStates);
            }
            
            if (id === "Types") {
                var unCheckedCbs = cbs.filter(function (index, cb) {
                    return !cb.checked;
                }, this);

                var excludes = "";
                var excludeArr = [];

                _.each(unCheckedCbs, function(cb) {
                    if (excludes > "") excludes += ", ";
                    excludes += cb.value;
                    excludeArr.push(cb.value);
                });

                this.model.Excludes(excludes);
                
                Main.trigger('excludes:updated', excludeArr);
            }
        },
        
        queryChanged:function (e) {
            this.model.Query(e.currentTarget.value);
        },
        
        queryDaysOutChanged: function(e) {
            this.model.QueryDaysOut(e.currentTarget.value);
        },
        
        simpleModeSelected:function () {
            this.ui.simple.removeClass('hide');
            this.ui.custom.addClass('hide');
        },
        
        customModeSelected:function () {
            this.ui.simple.addClass('hide');
            this.ui.custom.removeClass('hide');
        },
        
        selectFilters: function () {
            return App.request("getFilterPaths");
        },
        
        changePath:function(e) {
            this.model.IterationPath(e.currentTarget.value);
        }
    });


}); 