/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/

// define base module elements; other module files may depend
// on this, but it must not depend on any other module files
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {
    Main.prefix = "main";
    Main.templatePath = "modules/main/templates/";
    Main.views = {};
    Main.controllers = { };
    Main.models = { };
    Main.template = function(str) {
        return Main.prefix + '-' + str;
    };

    //Main.reqres = new Backbone.Wreqr.RequestResponse();
    
    Main.views.PageView = Marionette.Layout.extend({
        spin: function(on) {
            if (_.isUndefined(on) || on)
                this.ui.spinner.removeClass("hide");
            else
                this.ui.spinner.addClass("hide");
        }
    });
    
    
    // PLACEHOLDER. DO NOT REMOVE! When "Unifying" this module, external module files will be inserted here.

    this.startModule = function () {
        Marionette.ModuleHelper.loadModuleTemplates(Main, Main.show, window.AppIsReleased);
    };

    App.addInitializer(this.startModule);

});

// SECTION DELIMITER. DO NOT REMOVE! Code below this line will not be included in release mode.


// Recommended: define all dependencies for this module
// while you could spread dependency requirements
// over all your module files on purely "as needed" basis,
// this adds to complication of code in your module files
// defining them all, here, has the advantage of limiting use of RequireJS
// to this loader file only


define([
    "modules/main/models",
    "modules/main/controls/checkbox",
    "modules/main/views/header",
    "modules/main/views/footer",
    "modules/main/views/pageLayout",
    "modules/main/views/page_lklogin",
    "modules/main/views/boardList",
    "modules/main/views/navBar",
    "modules/main/views/page_targetLogin",
    "modules/main/views/tab_query",
    "modules/main/views/tab_laneMapping",
    "modules/main/views/tab_TypeMapping",
    "modules/main/views/tab_Options",
    "modules/main/views/mappingDetail",
    "modules/main/views/page_boardConfig",
    "modules/main/views/page_settings",
    "modules/main/views/page_activate",
    "modules/main/controller"
]);



