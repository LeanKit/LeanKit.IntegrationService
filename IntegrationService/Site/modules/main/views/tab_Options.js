/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.OptionsTabController = Marionette.Controller.extend({
        initialize: function (options) {
            this.model = options.model;
            this.view = new Main.views.OptionsTabView({ model: options.model });
        },
    });

    Main.views.OptionsTabView = NiceTools.BoundView.extend({
        template: this.template("tab_Options"),
        events: {
            "click #remove-btn": "removeMappingRequested",
            "click #cancel-btn": "canceled",
            "click #confirm-btn": "confirmed",
            "click #UpdateCards": "updateCardsChanged",
            "change fieldset input[type=checkbox]": "checkboxChanged"
        },
        ui: {
            "removeBtn": "#remove-btn",
            "confirmation": "#confirmation"
        },
        onShow: function() {
            this.bindModel();
            if (!this.$("#UpdateCards").prop("checked"))
                this.$("#UpdateCardLanes").prop("disabled", "disabled");
        },

        removeMappingRequested: function(e) {
            this.ui.removeBtn.addClass("hide");
            this.ui.confirmation.removeClass("hide");
        },
        
        canceled: function () {
            this.ui.removeBtn.removeClass("hide");
            this.ui.confirmation.addClass("hide");
        },
        
        confirmed: function () {
            this.model.destroy({ wait: true });
        },
        
        updateCardsChanged:function (e) {
            if(e.currentTarget.checked) {
                this.$("#UpdateCardLanes").prop("disabled","");
            } else {
                this.$("#UpdateCardLanes").prop("disabled", "disabled").prop("checked", false);
            }
        }
    });


});