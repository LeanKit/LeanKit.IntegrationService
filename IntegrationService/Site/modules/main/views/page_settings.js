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
            "earliestSyncDate": "#EarliestSyncDate",
            "pollingTime": "#PollingTime",
            "pollingUnits": "#PollingUnits",
            "pollingTimeReset": "#PollingTimeReset",
            "pollingRunOnce": "#PollingRunOnce"
        },

        initialize: function(options) {
            this.controller = options.controller;
            this.initializeBindings();
        },

        onShow: function() {
            this.bindModel();
            this.ui.earliestSyncDate.kendoDatePicker({ value: this.model.EarliestSyncDate() });
            this.ui.pollingTime.kendoTimePicker({ value: this.model.PollingTime(), interval: 60 });
            var bob = this.model;
            this.ui.pollingUnits.kendoDropDownList({
                dataSource: ["milliseconds", "seconds", "minutes", "hours"],
                value: this.model.PollingUnits(),
                change: function(e) {
                    bob.PollingUnits(this.value());
                }
            });
            var bill = this;
            this.ui.pollingTimeReset.click(function () {
                bill.ui.pollingRunOnce.checked(false);
                bill.model.PollingRunOnce(false);
                bill.ui.pollingTime.data("kendoTimePicker").value(null);
                bill.model.PollingTime(null);
            });
            this.ui.pollingRunOnce.checked = function() {
                if (bill.model.PollingRunOnce())
                    return true;
                return false;
            };
            this.ui.pollingRunOnce.change(function (e) {
                if (e.target.checked) {
                    bill.ui.pollingTime.data("kendoTimePicker").value(null);
                    bill.model.PollingTime(null);
                    bill.model.PollingRunOnce(true);
                } else {
                    bill.model.PollingRunOnce(false);
                }
            });
            if (App.Config.wizardMode)
                App.Config.pageValidated(this.controller.pageName, this.model, "Next: Configure Boards and Projects");
        },
        
        onChange: function () {
            this.model.save();
        }
    }, NiceTools.BoundViewMixIn));

});
