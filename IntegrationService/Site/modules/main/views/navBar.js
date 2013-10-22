/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {
    Main.views.NavBarView = Marionette.ItemView.extend({
        template: this.template("navBar"),
        events: { "click li a": "linkClicked" },
        className: "panel",
        displayPageNav: function (pages) {
            this.pages = pages;
            this.keys = _.keys(pages);
            var listItems = "";
            _.each(this.keys, function(key) {
                listItems += "<li class='disabled pointer' id='" + key + "'><a>" + pages[key] + "&nbsp;<span class='glyphicon glyphicon-ok hide'/></a></li>"; 
            });
            this.$("ul").html(listItems);
        },
        
        selectPage:function(pageId, enable, silently) {
            if (_.isUndefined(this.keys)) return;
            var self = this;
            var el;
            _.each(this.keys, function(key) {
                el = this.$("#" + key);
                if (el.hasClass('active')) {
                    el.removeClass('active');
                    if(!silently) self.trigger('page:deactivated', key);
                }
                if (key === pageId) {
                    // enable the nav button when selecting if 'enable' is true
                    if (enable && el.hasClass('disabled'))
                        el.removeClass('disabled');
                    el.addClass('active');
                    if (!silently) self.trigger('page:activated', key);
                }
            });
        },
        
        enable: function (pageId) {
            var el = this.$("#" + pageId);
            if (!_.isObject(el)) return;
            el.removeClass("disabled");
            this.trigger('page:enabled', pageId);
        },

        disable: function (pageId) {
            var el = this.$("#" + pageId);
            if (!_.isObject(el)) return;
            el.addClass("disabled");
            this.trigger('page:disabled', pageId);
        },

        linkClicked: function (e) {
            var el = $(e.currentTarget).parent();
            if(el.hasClass('disabled') || el.hasClass('active')) return;
            this.selectPage(el[0].id);
        },
        
        markPage: function (pageId, checked) {
            var el = this.$("#" + pageId);
            if (!el) return;
            el = el.find("a span");
            if (_.isUndefined(checked) || checked) {
                el.removeClass('hide');
                el.closest("li").removeClass("disabled");
            } else
                el.addClass('hide');

        }
    });

});