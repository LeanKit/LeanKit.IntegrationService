/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _, NiceTools) {


    Main.views.PageLayoutView = Marionette.Layout.extend({
        template: this.template("pageLayout"),
        regions: {
            nav: "#nav",
            page: "#pages",
            boardList: "#board-list"
        },

        ui: {
            "message": "#message",
            "next": "#btn-next"
        },

        initialize: function (options) {
            this.controller = options.controller;
        },

        templateHelpers: {
            login: function(val) {
                //return "bob";
                var html = view.render();
                var el = html.el;
                var innerHtml = el.innerHTML;
                return innerHtml;
            }
        },
        events: {
            "click #btn-next": "onNext"
        },
        
        onShow: function () {
            this.controller.viewIsShown();
        },

        showPage:function (view) {
            this.page.show(view);
        },
        
        onHideNext:function () {
            this.ui.next.addClass('hide');
        },
        
        onShowNext:function (msg) {
            this.ui.next.removeClass('hide');
            this.ui.next.html(msg);
        },
        
        onNext: function () {
            this.controller.nextPageRequested();
        }

    });

});
