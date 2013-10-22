/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {

    Main.models.CheckboxModel = Backbone.Model.extend({
        defaults: { Name: "", Checked: false },
        setChecked: function (val) {
            this.set("Checked", (val === true));
        },
        checked: function () {
            return this.get("Checked");
        }
    });

    Main.models.CheckboxCollection = Backbone.Collection.extend({
        model: Main.models.CheckboxModel,
    });


    Main.views.CheckboxItemView = Marionette.ItemView.extend({
        template: this.template("checkboxItem"),
        tagName: "label",
        className: "checkbox inline notbold pointer max100",

        ui: { "checkbox": "input[type=checkbox]" },

        initialize: function() {
            if (_.isUndefined(this.model.checked())) {
                this.model.set("Checked", false, { silent: true });
            }
        },

        id: function() {
            return this.model.get("Name").toId(false);
        },

        onRender: function () {
            if (this.model.checked())
                this.ui.checkbox.attr("checked", "checked");
            
        }
        
    });

    Main.views.CheckboxCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.CheckboxItemView,
    });
    
    Main.views.ButtonItemView = Marionette.ItemView.extend({
        template: this.template("buttonItem"),
        tagName: "div",
        className: "btn btn-default btn-xs margin-xs",

        id: function () {
            return this.model.get("Name").toId(false);
        },
        
        onRender:function () {
            this.$el.prop("title", this.model.get("Name"));
        }


    });

    Main.views.ButtonCollectionView = Marionette.CollectionView.extend({
        itemView: Main.views.ButtonItemView,
    });
});
