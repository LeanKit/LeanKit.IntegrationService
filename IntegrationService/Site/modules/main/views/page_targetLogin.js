/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.controllers.TargetLoginController = Marionette.Controller.extend({
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
            this.view = new Main.views.TargetLoginView({ model: this.model, controller: this });
        },

        onPageLeave: function () {
            this.view.close();
            this.view = null;
        },

        tryConnect: function () {
            App.log("tryConnect()");
            var self = this;
            var view = this.view;
            var url = "/tryconnection";
            view.spin();
            $.ajax(url, {
                type: 'GET',
                contentType: 'application/json',
                dataType: 'json',
                data: $.param(this.view.model.attributes),
                success: function () {
                    view.credentialsOK();
                    self.trigger('pageValidated', self.pageName, view.model, "Next: Check Global Settings");
                },
                error: function (collection, xhr) {
                    view.credentialsFailed();
                }
            });

        },

        getFormView: function () {
            // give formView to page view
            this.formView = new Main.views.TargetLoginFormView({ model: this.model, controller: this });
            return this.formView;
        }

    });


    Main.views.TargetLoginView = Main.views.PageView.extend({
        template: this.template("page_targetLogin"),
        className: "panel panel-primary",
        style:"min-height:400px",
        regions: {
            "form":"#form"
        },

        ui: {
            "message": "#message",
            "spinner": "#spinner"
        },

        initialize: function (options) {
            this.controller = options.controller;
        },

        onShow: function () {
            this.form.show(this.controller.getFormView());
        },

        credentialsOK: function () {
            this.spin(false);
            this.ui.message.html("Connection established to your " + this.model.get('Type') + " account.");
            this.$el.addClass("has-success").removeClass("has-error");
            this.form.currentView.hideConnect();
        },

        credentialsFailed:function () {
            this.spin(false);
            this.ui.message.html("Could not connect to " + this.model.get('Type') + ". Please verify your credentials.");
            this.$el.removeClass("has-success").addClass("has-error");
        }


    });

    Main.views.TargetLoginFormView = NiceTools.BoundView.extend({
        template: this.template("page_targetLogin_form"),
        tag: "form",
        className: "form-horizontal",
        initialize: function (options) {
            this.controller = options.controller;
        },

        ui: {
            "connect": "#btn-connect",
            "hostInput":"input#Host"
        },

        events: {
            "click ul.dropdown-menu li a":"protocolChanged",
            "click #btn-connect": "connectRequested",
            "change select#Type":"changeType"
        },

        onShow: function () {
            this.fadeHtmlChanges = false;
            this.bindModel();
            this.ui.hostInput.prop("placeholder", this.placeholder[this.model.Type()]);
        },

        onChange: function (m) {
            this.ui.connect.removeClass("disabled");
        },

        userPressedEnter: function () {
            var pwd = this.M("Password");
            if (_.isString(pwd) && pwd !== "")
                this.controller.tryConnect();
        },

        protocolChanged:function(e) {
            this.model.Protocol(e.currentTarget.text);
            this.$("#protocol-btn span.caption").text(e.currentTarget.text);
        },

        connectRequested: function () {
            this.controller.tryConnect();
        },

        changeType:function (e) {
			var target = this.$(e.currentTarget).val();
            this.ui.hostInput.prop("placeholder", target);
        },

        hideConnect: function () {
            this.ui.connect.hide();
        },

        placeholder: {
            TFS: "YourServer.com/YourCollection",
            Jira: "YourAccount.atlassian.net",
            GitHubIssues:"GitHub Account Name",
            GitHubPulls:"GitHub Account Name"
        }

    });

});
