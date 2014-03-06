/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {


    Main.controllers.MappingDetailController = Marionette.Controller.extend({
        initialize: function(options) {
            this.owner = options.owner;
            this.model = options.model;
            this.filterfields = options.filterfields;
            this.configureViews();
            
            if (this.model.TargetProjectId() !== "") {
                this.view = this.loadBoardDetails();
            } else {
                this.view = new Main.views.MappingDetailView({ controller: this, model: this.model });
            }


        },

        loadBoardDetails: function() {
            // look up the board details
            this.loading = $.Deferred();
            this.board = new Main.models.Board();
            var self = this;
            var parms = App.credentials.leankit.asQueryString("&boardId=" + this.model.BoardId());
            this.board.fetch({
                data: parms,
                success: function(item) {
                    if (_.isUndefined(self.view.render)) {
                        self.view = new Main.views.MappingDetailView({ controller: self, model: self.model });
                        self.loading.resolve();
                    } else {
                        self.owner.triggerMethod('board:loaded');
                    }
                }
            });


            return this.loading;

        },

        configure: function () {
            this.owner.triggerMethod('project:assigned');
            this.loadBoardDetails();
        },

        close:function() {
            if (_.isObject(this.view)) {
                this.view.close();
            }
            this.view = undefined;
            this.model = undefined;

        },
        
        configureViews: function() {
            this.viewFactory = new App.ViewFactory(this, "MappingDetail");

            this.viewFactory.register("optionsTab", function (c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = {};
                if (_.isObject(c.subcontrollers.optionsTab)) {
                    c.subcontrollers.optionsTab.close();
                    c.subcontrollers.optionsTab = undefined;
                }

                c.subcontrollers.optionsTab = new Main.controllers.OptionsTabController({ model:c.model});
                return c.subcontrollers.optionsTab.view;
            });

            this.viewFactory.register("queryTab", function (c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = {};
                if (_.isObject(c.subcontrollers.queryTab)) {
                    c.subcontrollers.queryTab.close();
                    c.subcontrollers.queryTab = undefined;
                }

                c.subcontrollers.queryTab = new Main.controllers.QueryTabController({ model: c.model, targetTypes: c.owner.getTypes() });
                return c.subcontrollers.queryTab.view;
            });

            this.viewFactory.register("laneMapTab", function(c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = { };
                if (_.isObject(c.subcontrollers.laneMap)) {
                    c.subcontrollers.laneMap.close();
                    c.subcontrollers.laneMap = undefined;
                }

                c.subcontrollers.laneMap = new Main.controllers.LaneStateMappingController({ owner: c, model:c.model, board: c.board, lanesAndStates: c.model.LaneToStatesMap() });
                return c.subcontrollers.laneMap.view;
            });

            this.viewFactory.register("fieldMapTab", function (c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = {};
                if (_.isObject(c.subcontrollers.fieldMapTab)) {
                    c.subcontrollers.fieldMapTab.close();
                    c.subcontrollers.fieldMapTab = undefined;
                }

                c.subcontrollers.fieldMapTab = new Main.controllers.FieldMapTabController({ owner: c, board: c.board, configuration: c.model.FieldMap() });
                return c.subcontrollers.fieldMapTab.view;
            });

            this.viewFactory.register("typeMapTab", function (c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = { };
                if (_.isObject(c.subcontrollers.typeMapTab)) {
                    c.subcontrollers.typeMapTab.close();
                    c.subcontrollers.typeMapTab = undefined;
                }

                var leanKitTypes = [];
                if (_.isObject(c.board)) {
                    var attrs = _.pluck(c.board.CardTypes().models, "attributes");
                    leanKitTypes = _.pluck(attrs, "Name");
                }
                var targetTypes = _.pluck(c.owner.getTypes(), "Name");

                c.subcontrollers.typeMapTab = new Main.controllers.TypeMapTabController({ owner: c, board: c.board, collection: c.model.TypeMap(), leanKitTypes: leanKitTypes, targetTypes: targetTypes });
                return c.subcontrollers.typeMapTab.view;
            });
            
            this.viewFactory.register("filtersTab", function (c) {
                if (!_.isObject(c.subcontrollers)) c.subcontrollers = {};
                if (_.isObject(c.subcontrollers.filtersTab)) {
                    c.subcontrollers.filtersTab.close();
                    c.subcontrollers.filtersTab = undefined;
                }

                c.subcontrollers.filtersTab = new Main.controllers.FiltersTabController({ owner: c, board: c.board, configuration: c.model.Filters(), filterfields: c.filterfields });
                return c.subcontrollers.filtersTab.view;
            });

            this.viewFactory.register("projectPicker", function(controller) {
                var availableProjects = controller.owner.getAvailableTargetProjects();
                return new Main.views.SelectView({ id: "TargetProjectId", collection: availableProjects });
            });
        },

        onPrepNestedViews: function () {
            this.viewFactory.each(function (nestedView) {
                if (_.isString(nestedView.id)) {
                    var region = this.view[nestedView.id];
                    region.show(nestedView);
                }
            }, this);
        }
        
    });

    Main.views.MappingDetailViewMixIn = {
        template: this.template("mappingDetail"),
        tag: "div",
        className: "panel panel-primary",
        style: "margin-left:20px",
        noProjectEvents: {
            "change fieldset select":   "selectChanged",
            "click #btn-configure":     "configureRequested"
        },

        projectEvents: {
            "click .nav li a":          "tabClicked",
            "click #save-btn":          "saveRequested"
        },
        
        initialize: function (options) {
            this.controller = options.controller;
            this.model = options.model;
            this.pickerView = undefined;
            this.listenTo(this.model, "change", this.modelChanged2, this);
            this.resetEvents();
            this.bindUIElements();
        },

        resetEvents:function (delegateNow) {
            this.events = this.model.TargetProjectId() === "" ? this.noProjectEvents : this.projectEvents;
            if (delegateNow) this.delegateEvents();
        },
        
        ui: {
            "projectPicker": "#picker",
            "controls": "#controls",
            "notMapped": "#not-mapped",
            "configure": "#btn-configure",
            "tabstrip": "#tabstrip",
            "saveBtn": "#save-btn",
            "errorIcon": "#error-icon"
        },

        onShow: function () {
            if (this.model.hasProject()) {
                this.ui.notMapped.addClass('hide');
                this.ui.controls.removeClass('hide');
            } else {
                this.ui.notMapped.removeClass('hide');
                this.ui.controls.addClass('hide');
            }

            this.ui.saveBtn.popover({ trigger: "hover", title: "This mapping has changes", content: "You can save them now, or they will be automatically saved when selecting a new project or section.", placement: "bottom" });
            this.ui.errorIcon.popover({ trigger: "hover", title: "This mapping is incomplete", content: "You must have at least one status selected on the 'Selection' tab, and each selection status must be assigned to a lane on the 'Lane and States' tab.", placement: "bottom" });

            this.ui.tabstrip.kendoTabStrip({ activate:this.tabActivated, animation: { open: { effects: "" } } });

            var tabStrip = this.ui.tabstrip.kendoTabStrip().data("kendoTabStrip");
            if (this.model.FieldMap().length > 0) {
                tabStrip.enable(tabStrip.items()[2], true);
                tabStrip.enable(tabStrip.items()[1], false);
                tabStrip.enable(tabStrip.items()[3], false);
                tabStrip.enable(tabStrip.items()[5], false);
            } else {
                tabStrip.enable(tabStrip.items()[2], false);
                tabStrip.enable(tabStrip.items()[1], true);
                tabStrip.enable(tabStrip.items()[3], true);
                tabStrip.enable(tabStrip.items()[5], true);
            }
            if (!_.isUndefined(this.controller.filterfields) && this.controller.filterfields.length > 0) {
                tabStrip.enable(tabStrip.items()[4], true);
            } else {
                tabStrip.enable(tabStrip.items()[4], false);
            }

            this.controller.triggerMethod("prep:nestedViews");

            this.bindModel();

            this.validate();
        },

        tabActivated: function (e) {
            // trigger backbone event with name of tab that was activated
            Main.trigger('tab:activated', e.item.textContent);
        },
        
        onItemSelected: function (id, label, target) {
            this.ui.configure.removeClass('disabled');
            this.model.set("TargetProjectName", label);
            // TargetProjectId is set automatically in BoundView
        },

        configureRequested: function () {
            this.controller.configure();
            this.resetEvents(true);
        },

        tabClicked: function (e) {
            e.preventDefault();
            $(this).tab('show');
        },

        modelChanged2: function () {
            this.validate(true);
        },

        validate: function (hasChanges) {
            if (this.model.isValid()) {
                this.ui.errorIcon.addClass('hide');
                if (hasChanges) {
                    this.ui.saveBtn.removeClass('hide');
                    this.ui.saveBtn.fadeIn();
                }
            } else {
                this.ui.saveBtn.addClass('hide');
                this.ui.errorIcon.removeClass('hide');
                this.ui.errorIcon.fadeIn();
            }
        },


        saveRequested: function () {
            var saved = App.request("saveCurrentMapping");
            if (saved) {
                this.ui.saveBtn.fadeOut();
            }
        }


    };

    Main.views.MappingDetailView = Marionette.Layout.extend(
        _.extend(Main.views.MappingDetailViewMixIn, NiceTools.BoundViewMixIn));

});