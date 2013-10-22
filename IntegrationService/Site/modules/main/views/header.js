/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {
    Main.views.HeaderView = Marionette.ItemView.extend({
        template: this.template("header"),
        tagName: "div",
        className: "well toolbar"
    });

});