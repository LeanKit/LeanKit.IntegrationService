/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {


    Main.controllers.LaneStateMappingController = Marionette.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;

            //this.customStateCollection = new Backbone.Collection();
            //this.listenTo(this.customStateCollection, "remove", this.customStateRemoved, this);
            //App.commands.setHandler('customStateAdded', this.customStateAdded, this);
            this.model = options.model;
            if (_.isObject(options.board)) {
                this.model.set("LaneHtml", options.board.LaneHtml(), { silent: true });
            }
            this.lanesAndStates = options.lanesAndStates;
            this.currentStates = new Backbone.Collection();
            this.listenTo(this.currentStates, "remove", this.stateRemoved, this);

            this.workflowStates = new Backbone.Collection();
            
            this.configureViews();
            App.reqres.setHandler('getStatesForPicker', this.getStatesForPicker, this);
            
            // subscribe to tab:activated events
            this.listenTo(Main, 'tab:activated', this.tabActivated, this);
        },

        getStatesForPicker:function () {
            return this.statesForPicker;
        },
        
        configureViews: function () {
            this.view = new Main.views.LaneStateMappingView({ controller: this, model:this.model });
            
            this.viewFactory = new App.ViewFactory(this, "laneStateMap");

            this.viewFactory.register("StateButtons", function (c) {
                var states = App.request("getStates");
                c.statesForPicker = _.pluck(states, "Name");
                c.statesForPicker.unshift("");
                c.stateCollection = new Backbone.Collection(states);
                var view = new Main.views.ButtonCollectionView({ id: "StateButtons", collection: c.stateCollection });
                
                return view;
            });

            this.viewFactory.register("States", function(c) {
                var view = new Main.views.StateCollectionView({ id: "States", collection: c.currentStates });
                return view;
            });
            
            this.viewFactory.register("CustomStates", function(c) {
                var view = new Main.views.CustomStateCollectionView({ id: "CustomStates", collection: c.customStateCollection });
                return view;
            });

            this.viewFactory.register("WorkflowStates", function(c) {
                var view = new Main.views.StateCollectionView({ id: "WorkflowStates", collection: c.workflowStates });
                return view;
            });
            
        },
        
        onPrepViews:function () {
            this.viewFactory.each(function(view) {
                if (_.isString(view.id)) {
                    var id = "#" + view.id;
                    var $el = this.view.$(id);
                    if (_.isObject($el)) {
                        view.$el = $el;
                        view.bindUIElements();
                        view.delegateEvents();
                        //view.triggerMethod("prep:Views");
                    }
                }
            }, this);
        },

        laneSelected: function (laneId) {
            this.currentLane = laneId;
            var stateArr = this.model.LaneToStatesMap()[laneId];
            if (_.isUndefined(stateArr)) {
                stateArr = [];
            }
            var states = [];
            _.each(stateArr, function(state) {
                states.push({ State: state });
            }, this);
            
            this.currentStates.reset(states);

        },
        
        stateAdded: function (state, addToWorkFlow) {
            if (addToWorkFlow) {
                this.workflowStates.add(new Backbone.Model({ State: state }));
            } else {
                this.currentStates.add(new Backbone.Model({ State: state }));
                this.owner.model.addStateToLane(this.currentLane, state);
                this.view.markUnassignedStatesAsWarning(this.model.unassignedStates);
            }
        },
        
        stateRemoved: function (model) {
            var state = model.get("State");
            this.owner.model.removeStateFromLane(this.currentLane, state);
            this.view.markUnassignedStatesAsWarning(this.model.unassignedStates);
        },

        useWorkflow: function () {
            if (this.workflowStates.length === 0) return;
            var workflowState = "";
            this.workflowStates.each(function(model) {
                workflowState += model.get("State") + ">";
            }, this);
            workflowState = workflowState.substring(0, workflowState.length-1);
            this.stateAdded(workflowState);
            this.workflowStates.reset();
        },
        
        resetWorkflowStates: function () {
            this.workflowStates.reset();
        },
        
        addCustomState: function (state) {
            this.customStateCollection.add(new Backbone.Model({ State: state }));
        },
        
        //customStateAdded: function (state) {
        //    this.owner.model.addStateToLane(this.currentLane, state);
        //},
        
        //customStateRemoved: function (m) {
        //    this.owner.model.removeStateFromLane(this.currentLane, m.get("State"));
        //},
        
        tabActivated:function (tabName) {
            if (tabName !== "Lanes and States") return;
            // if the required states differ from queryStates, update required states and re-validate
            if(this.requiredStates !== this.model.QueryStates()) {
                this.requiredStates = this.model.QueryStates();
                // validate
                this.model.isValid();
                this.view.markUnassignedStatesAsWarning(this.model.unassignedStates);
            }
        }


    });

    Main.views.NestedView = Marionette.ItemView.extend({
        initialize:function (options) {
            this.controller = options.controller;
            this.triggerMethod("after:initialize", options);

        },
       onRender:function () {
           if (this.controller) this.controller.triggerMethod("prep:views");
           this.triggerMethod("rendered");
       } 
    });

    Main.views.LaneStateMappingView = Main.views.NestedView.extend({
        id: "tab_laneMapping",
        template: this.template("tab_laneMapping"),

        events: {
            "click div.kb-ch-laneHeaderRepresentation": "laneSelected",
            "click #StateButtons div.btn": "stateSelected",
            "change div#workflow textarea": "workflowChanged",
            "click #build-workflow": "buildWorkflowToggled",
            "click #use-workflow": "useWorkflowRequested"
        },

        ui: {
            "lanetable": "#lanetable",
            "states": "#States",
            "helpBoardLanes": "#help-board-lanes",
            "helpProjectStates": "#help-project-states",
            "htlpAvailableStates": "#help-available-states",
            "buildWorkflowBtn": "#build-workflow",
            "useWorkflowBtn": "#use-workflow",
            "stateEditControls": "#state-edit-controls"
        },

        onRendered: function() {

            this.ui.helpBoardLanes.popover({ trigger: "hover", title: "Board Lanes", content: "Select a lane to manage associated project states.", placement: "left" });
            this.ui.helpProjectStates.popover({ trigger: "hover", title: "Mapped States", content: "These are the states (and workflows) associated with the selected lane. If you select multiple states, the first matched state or workflow will be applied.", placement: "left" });
            this.ui.htlpAvailableStates.popover({ trigger: "hover", title: "Available States", content: "Click to associate a state with the selected lane. To build a workflow, enable 'Build Workflow', then add states to the workflow and confirm by clicking the checkmark button.", placement: "left" });

            this.ui.states.addClass("hide");
            this.controller.triggerMethod("ready");

            this.buildWorkflow = false;
            this.ui.useWorkflowBtn.addClass('disabled');

        },

        onShow:function () {
            
        },
        
        laneSelected: function(e) {

            // radio-select this lane
            $("div.kb-ch-laneHeaderRepresentation").removeClass("k-state-active");
            $(e.currentTarget).addClass("k-state-active");

            // show state checkboxes
            this.$("#States").removeClass("hide");

            // set them all to unchecked
            this.$("#States input").prop("checked", false);

            // get states for the selected lane
            this.controller.laneSelected(parseInt(e.currentTarget.id));

            // show controls
            this.ui.stateEditControls.removeClass('hide');
        },

        buildWorkflowToggled: function(e) {
            this.buildWorkflow = e.currentTarget.checked;
            if (this.buildWorkflow) {
                this.ui.useWorkflowBtn.removeClass('disabled');
            } else {
                this.controller.resetWorkflowStates();
            }
        },

        useWorkflowRequested: function() {
            this.controller.useWorkflow();
            this.buildWorkflow = false;
            this.ui.buildWorkflowBtn.prop("checked", "");
            this.ui.useWorkflowBtn.addClass('disabled');
        },

        stateSelected: function(e) {
            this.controller.stateAdded(e.currentTarget.id.fromId(), this.buildWorkflow);
        },
        
        markUnassignedStatesAsWarning: function (unassignedStates) {
            this.$("div#StateButtons .btn").removeClass('btn-warning').addClass('btn-default');
            if (!_.isArray(unassignedStates)) return;

            _.each(unassignedStates, function(state) {
                this.$(state.toId()).removeClass('btn-default').addClass('btn-warning');
            }, this);
        }
    });



    Main.views.StateItemView = Marionette.ItemView.extend({
        template: this.template("stateItem"),
        events: {
            "click span": "removeRequested"
        },
        className: "tag",
               
        removeRequested:function () {
            this.model.destroy();
        }
    });
    
    Main.views.StateCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.StateItemView
    });
    
});


