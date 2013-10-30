/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.SettingsController = Marionette.Controller.extend({
        initialize: function (options) {
            App.log("settings.initialize");
            this.owner = options.owner;
            this.pageName = options.pageName;
            this.model = options.model;
        },
        
        isValid: function () {
            if (!this.model) return false;

            return this.model.isValid();
        },
                    
        onPageShow:function () {
            App.log("settings.onPageShow");
            this.view = new Main.views.SettingsView({ model: this.model, controller: this });
        },
        
        onPageLeave:function () {
            App.log("settings.onPageLeave");
            this.view.close();
            this.view = null;
        }
        
    });
    

    Main.views.SettingsView = Main.views.PageView.extend(_.extend({
        template: this.template("page_settings"),
        className:"panel panel-primary",
        ui: {
            "earliestSyncDate":"#EarliestSyncDate"
        },

        initialize: function(options) {
            this.controller = options.controller;
            this.initializeBindings();
        },

        onShow: function() {
            this.bindModel();
            this.ui.earliestSyncDate.kendoDatePicker({ value: this.model.EarliestSyncDate() });
            if (App.Config.wizardMode)
                App.Config.pageValidated(this.controller.pageName, this.model, "Next: Configure Boards and Projects");
        },
        
        onChange: function () {
            this.model.save();
        }
    }, NiceTools.BoundViewMixIn));

});
