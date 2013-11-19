module.exports = function (grunt) {
    grunt.unify = {};
    grunt.handlebars = {};
    // extract just the file name from the full path
    grunt.handlebars.simpleName = function (filePath) {
        var fileParts = filePath.split('/');
        var fileNameParts = fileParts[fileParts.length - 1].split('.');
        return fileNameParts[0];
    };
    grunt.initConfig({
        pkg: grunt.file.readJSON('package.json'),
        uglify: {
            options: {
                banner: '/*! <%= pkg.name %> <%= grunt.template.today("yyyy-mm-dd") %> */\n'
            },
            main: {
                files: {
                    '../bin/release/site/app.js':
                        ['../site/releaseMode.js', '../site/app.js'],
                    
                    '../bin/release/site/main.min.js':
                        [
                        '../site/generated/boardModel.js',
                        '../site/generated/Models.js',
                        '../site/generated/main.js']
                }
            },
        },
        handlebars: {
            main: {
                options: {
                    namespace: 'Handlebars.main',
                    processName: grunt.handlebars.simpleName
                },
                files: {
                    'temp-main-templates.js': '../site/modules/main/templates/*.html'
                }
            },
        }
    });

    grunt.loadNpmTasks('grunt-contrib-handlebars');
    grunt.loadNpmTasks('grunt-contrib-uglify');

    grunt.registerTask('compile', ['handlebars:main']);
    grunt.registerTask('unify-all', ['unify:main']);
    grunt.registerTask('default', ['compile', 'unify-all', 'uglify', 'clean']);

    grunt.registerTask('clean', 'Remove dev-time files for release.', function() {
        console.log('clean for release');
        grunt.file.setBase('../bin/release/site');
        grunt.file.delete('generated');
        grunt.file.delete('modules');
        grunt.file.delete('Views');
    });
    
    grunt.registerTask('unify', 'Convert a set of Marionette Module files to a single file', function (modName) {
        console.log('unify marionette module: ' + modName);
        var loader = '../site/modules/' + modName + '/loader.js';
        var code = grunt.file.read(loader);
        var placeholder = code.indexOf("// PLACEHOLDER.");
        var placeholderEnd = code.indexOf("\n", placeholder);
        var sectionDelim = code.indexOf("// SECTION DELIMITER.");

        if (placeholder === -1 || sectionDelim === -1) {
            console.log('Missing placeholder ["// PLACEHOLDER."] or section delimiter ["// SECTION DELIMITER."] comment markers.');
            return;
        }

        var prefixCode = code.substr(0, placeholder - 1);
        var start = placeholderEnd + 1;
        var len = sectionDelim - start;
        var suffixCode = code.substr(start, len);

        grunt.unify.moduleCode = "";

        // add generated templates
        var templatesFile = 'temp-' + modName + '-templates.js';
        if (grunt.file.exists(templatesFile)) {
            console.log('Adding module templates: ' + templatesFile);
            grunt.unify.moduleCode += '\n' + grunt.file.read(templatesFile);
            grunt.file.delete(templatesFile);
        }

        grunt.unify.moduleCode += prefixCode;
        grunt.file.recurse('../site/modules/' + modName, grunt.unify.addModuleFile);
        grunt.unify.moduleCode += suffixCode;

        grunt.file.write('../site/generated/' + modName + '.js', grunt.unify.moduleCode);

    });

    grunt.unify.addModuleFile = function (abspath, rootdir, subdir, filename) {
        if (filename === 'loader.js') return;
        var parts = filename.split('.');
        if (parts[1] === 'js') {
            console.log('  Adding ' + filename);
            grunt.unify.moduleCode += grunt.unify.getModuleCode(abspath);
        }
    };

    grunt.unify.getModuleCode = function (file) {
        var code = grunt.file.read(file);
        var moduleStart = code.indexOf("App.module(");
        var i = code.indexOf('\n', moduleStart);
        code = code.substring(i);
        i = code.lastIndexOf('});');
        code = code.substring(0, i);
        return code;
        //var ch = code.charCodeAt(code.length - 1, 1);
        //if (ch !== 10) {
        //    code += '\n';
        //}
        //var lines = code.split('\r\n');
        //code = "";
        //var skippingFirst = true;
        //for (var i = 0; i < lines.length - 2; i++) {
        //    if (lines[i] === '') continue;
        //    if (skippingFirst) {
        //        skippingFirst = false;
        //        continue;
        //    }
        //    code += lines[i] + '\r\n';
        //}
        //return code;
    };

};

