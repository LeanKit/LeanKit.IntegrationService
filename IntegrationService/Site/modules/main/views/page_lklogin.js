/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.LeanKitLoginController = Marionette.Controller.extend({
        initialize: function (options) {
            this.owner = options.owner;
            this.pageName = options.pageName;
            this.model = options.model;
        },
        
        isValid: function () {
            if (!this.model) return false;

            return this.model.isValid();
        },
                       
        onPageShow: function () {
            this.view = new Main.views.LeanKitLoginView({ model: this.model, controller: this });
        },

        onPageLeave: function () {
            this.view.close();
            this.view = null;
        },


        tryConnect: function () {
            App.log("tryConnect()");
            // try fetching the boards collection
            var boards = new Main.models.BoardCollection();
            this.view.spin();
            var view = this.view;
            var self = this;
            boards.fetch({
                data: $.param(this.view.model.attributes),
                success: function (collection) {
                    view.credentialsOK(collection.length);
                    self.trigger('pageValidated', self.pageName, view.model, "Next: Connect To Target");
                },
                error: function (collection, xhr) {
                    view.credentialsFailed();
                }
            });

        },
        
        getFormView: function () {
            // give formView to page view
            this.formView = new Main.views.LeanKitLoginFormView({ model: this.model, controller: this });
            return this.formView;
        }
    });

    

    Main.views.LeanKitLoginView = Main.views.PageView.extend({
        template: this.template("page_lklogin"),
        className: "panel panel-primary",
        regions: {
            "form":"#form"
        },
        
        ui: {
            "message": "#message",
            "spinner": "#spinner",
            "form":"#form"
        },


        initialize: function (options) {
            this.controller = options.controller;
        },

        onShow: function () {
            this.form.show(this.controller.getFormView());
        },
        
        credentialsOK: function (collectionLength) {
            this.spin(false);
            this.ui.message.html("Connection established to your LeanKit account.<br>Boards available: " + collectionLength);
            this.$el.addClass("has-success").removeClass("has-error");
            this.form.currentView.hideConnect();
        },
        
        credentialsFailed:function () {
            this.spin(false);
            this.ui.message.html("Could not connect to LeanKit. Please verify your credentials.");
            this.$el.removeClass("has-success").addClass("has-error");
        }

                
        
    });


    Main.views.LeanKitLoginFormView = NiceTools.BoundView.extend({
        template: this.template("page_lklogin_form"),
        tag: "form",
        className: "form-horizontal",
        initialize: function (options) {
            this.controller = options.controller;
        },

        ui: {
            "connect": "#btn-connect",
        },

        events: {
            "click #btn-connect": "connectRequested"
        },

        onShow: function () {
            this.bindModel();
        },

        onChange: function (m) {
            this.ui.connect.removeClass("disabled");
        },

        userPressedEnter: function () {
            var pwd = this.M("Password");
            if (_.isString(pwd) && pwd !== "")
                this.controller.tryConnect();
        },

        connectRequested: function () {
            this.controller.tryConnect();
        },

        hideConnect: function () {
            this.ui.connect.hide();
        }

    });

});
