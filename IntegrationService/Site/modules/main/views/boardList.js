/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    
    Main.views.BoardItemView = Marionette.ItemView.extend({
        template: this.template("boardItem"),
        tagName: "a",
        id:function () {
            return this.model.get("Id");
        },
        className: "list-group-item pointer",
        templateHelpers: {
            syncArrows:function() {
                // return "<span class='glyphicon glyphicon-arrow-left'/>&nbsp;<span class=glyphicon glyphicon-arrow-right'/>";
                return this.syncDirection;
            }
        }
    });

    Main.views.BoardListView = Marionette.CollectionView.extend({
        itemView: Main.views.BoardItemView,
        tagName: 'dir',
        className: "list-group list-group-flush",
        events: {
 
        }
    });

    Main.views.SelectItemView = Marionette.ItemView.extend({
        template: this.template("selectItemView"),
        tagName: "option",
        //id: function () {
        //    return this.model.get("Id");
        //},
        onRender: function() {
            this.$el.prop("value", this.model.get("Id"));
        }
    });

    Main.views.SelectView = Marionette.CollectionView.extend({
        itemView: Main.views.SelectItemView,
        tagName: 'select',
        className: "form-control"
    });



});
