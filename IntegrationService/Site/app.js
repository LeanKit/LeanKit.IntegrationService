
require.config({
    paths: {
        underscore: 'lib/underscore',
        bootstrap:'lib/bootstrap',
        backbone: 'lib/backbone',
        marionette: 'lib/marionette',
        modulehelper: 'lib/modulehelper',
        nicetools: 'lib/nicetools',
       // cocktail: 'lib/cocktail',
        //kendo: '//cdn.kendostatic.com/2013.2.716/js/kendo.all.min',
        "kendo.all.min": 'lib/kendo.all.min',
        //"kendo.core.min": 'lib/kendo.core.min',
        //kendopanelbar:'lib/kendo.panelbar.min',
        handlebars:(function () {
            var path = 'lib/handlebars';
            if (window.AppIsReleased) path += '.runtime';
            return path;
        })()
    },
    shim: {
        underscore: {
            exports: '_'
        },
        bootstrap: {
            deps: ["jquery"],
            
        },
        backbone: {
            deps: ["underscore", "jquery"],
            exports: "Backbone"
        },
        marionette: {
            deps: ["backbone"],
            exports:"Marionette"
        },
        modulehelper: {
            deps: ["marionette"]
        },
        nicetools: {
            deps: ["marionette", "bootstrap"],
            exports: "NiceTools"
        },
        kendopanelbar: {
            deps: ["kendo.core.min"],
            exports: "kendopanelbar"
        },
        //cocktail: {
        //    deps: ["marionette"],
        //    exports: "cocktail"
        //}
    }
});


require(["marionette", "nicetools", "handlebars", "modulehelper", "kendo.all.min"], function (Marionette, NiceTools, hbr, mh, k) {
    window.App = new Marionette.Application();

    App.codegen = {}; // used for generated models
    
    App.addRegions({
        header: "#header-region",
        footer: "#footer-region",
        body1: "#body1-region",
        body2: "#body2-region"
    });
    
    App.modules = function (name, arr) {
        if (_.isUndefined(arr)) arr = [];
        if (_.isObject(name)) {
            _.each(name, function (thisName) {
                App.modules(thisName, arr);
            });
            return arr;
        }
        if (window.AppIsReleased)
            arr.push(name + '.min');
        else {
            arr.push("generated/boardmodel");
            arr.push("generated/models");
            arr.push("modules/" + name + "/loader");
        }
        
        return arr;
    };

    require(App.modules("main"), function () {
        App.start();
    });

    App.log = function(str) {
        if (!console) return;
        console.log(str);
    };
    
    App.ViewFactory = NiceTools.ViewFactory.extend({
        initialize: function (options) {
            // when an id is provided, App.request('<id>') can be used to retrieve this factory
            if (this.id) {
                var self = this;
                App.reqres.setHandler(this.id, function () {
                    return self;
                });

            } else {

                // stash this viewFactory when created
                App.viewFactory = this;
            }
        }
    });

    Handlebars.registerHelper('partial', function (viewId, block) {
        var view;
        var html = "";
        
        // try to get the view factory
        var vfactory;
        if(_.isString(block.hash.factory))
            vfactory = App.request(block.hash.factory);
        else if (_.isObject(this._c))
            vfactory = this._c.viewFactory;
        else if(_.isObject(App.viewFactory))
            vfactory = App.viewFactory;

        if (_.isObject(vfactory)) {
            view = vfactory.createView(viewId);
            if (_.isObject(view)) {
                var el = view.render().el;
                html = el.outerHTML;
                view.close();
                vfactory.remove(view);
            }
        }
        return html;
    });

    Handlebars.registerHelper('nestedView', function (viewId, block) {
        var view;
        var html = "";
        
        // try to get the view factory
        var vfactory;
        if(_.isString(block.hash.factory))
            vfactory = App.request(block.hash.factory);
        else if (_.isObject(this._c))
            vfactory = this._c.viewFactory;
        else if(_.isObject(App.viewFactory))
            vfactory = App.viewFactory;

        if (_.isObject(vfactory)) {
            vfactory.createView(viewId);
            html = "<div id='" + viewId + "'";
            if (_.isString(block.hash.class))
                html += " class='" + block.hash.class + "'";
            html += "></div>";
        }

        return html;
    });

    Handlebars.registerHelper("checkIf", function (ifFunction, options) {
        ifFunction = "checkIf_" + ifFunction;
        var result = App.request(ifFunction, this);
        if(result) {
            return options.fn(this);
        } else {
            return options.inverse(this);
        }

    });
    
    Handlebars.registerHelper("request", function (request, options) {
        var result = App.request(request, options, this);
        return result;
    });
    
    Handlebars.registerHelper('simpleSelect', function (itemFunction, isSelectedFunction, options) {
        var html = "";
        var items = undefined;
        if (_.isString(itemFunction)) {
            items = App.request(itemFunction, this);
        }
        
        if (!_.isArray(items)) return "No data for " + itemFunction + " selector";

        html += "<select";
        var hashKeys = _.keys(options.hash);
        _.each(hashKeys, function(key) {
            html += " " + key + "='" + options.hash[key] + "'";
        });
        html += ">";

        var model = this;
        for (var index in items) {
            html += String.format("<option value='{0}' {2}>{1}</option>", items[index], items[index],
                function () {
                    if (_.isString(isSelectedFunction)) {
                        var res = App.request(isSelectedFunction, model, index);
                        if (res) {
                            return "selected";                        
                        } 
                    }
                    return "";
                });
        }

        html += "</select>";
        
        return html;
    });

    Handlebars.registerHelper('truncate', function (property, block) {
        var val = this[property];
        if (val.length <= block.hash.size) return val;
        
        return val.substr(0, block.hash.size) + "...";
    });


});

