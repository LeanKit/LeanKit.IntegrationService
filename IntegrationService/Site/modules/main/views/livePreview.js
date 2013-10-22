/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {


    Main.controllers.LivePreviewController = Marionette.Controller.extend({
        initialize: function(options) {
            this.owner = options.owner;
            this.mapping = options.mapping;
            this.view = new Main.views.LivePreviewView();
        },
        


    });
    
    Main.views.LivePreviewView = Marionette.Layout.extend({
        template: this.template("livePreview"),
        events: {
        },
        
        initialize: function (options) {
            this.controller = options.controller;
        },
        
        ui: {
        },

        onShow: function () {

        },

        
    });

});
