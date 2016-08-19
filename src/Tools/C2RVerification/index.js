/*
 *  To run 
 *  1. Install node.js from: https://nodejs.org/en/download/, skip this if you have node installed
 *  2. In C2RVerification folder run below command to install dependencies
 *          npm install
 *  3. Execute below command to invoke folder comparison.
 *          node index.js <Path to Registration folder1> <Path to Registration folder2> [optional: -showNonJsonFiles]
 *      eg: node index.js e:\nuget\assets\reg0-graph e:\nuget\assets\reg0-rawjson -showNonJsonFiles
 */

var fs = require("fs");
var dirCompare = require("dir-compare");
var showJsonFiles = false;
var deepEqual = require("deep-equal");
var ignoreKeys = ["commitTimeStamp", "commitId"];

function run() {
    var dirPath1 = process.argv[2];
    var dirPath2 = process.argv[3];
    showNonJsonFiles = process.argv[4] == "-showNonJsonFiles";

    if (!dirPath1 || !dirPath2) {
        console.log("Missing path to folders. Usage: node index.js <Path to Registration folder1> <Path to Registration folder2>");
        return;
    }

    if (!fs.lstatSync(dirPath1).isDirectory() || !fs.lstatSync(dirPath2).isDirectory()) {
        console.log("Please provide path to the folders to compare. Usage: node index.js <Path to Registration folder1> <Path to Registration folder2>");
        return;
    }

    compare(dirPath1, dirPath2);
}

function fileComparator(filePath1, stat1, filePath2, stat2) {
    if (!/.*\.json$/.test(filePath1) || !/.*\.json$/.test(filePath2)) {
        if (showNonJsonFiles) {
            console.log("[Warning] Not a json file:" + filePath1);
            console.log("[Warning] Not a json file:" + filePath2);
        }

        return stat1.size == stat2.size;
    }

    var contentsOfFile1 = require(filePath1);
    var contentsOfFile2 = require(filePath2);

    var result = deepEqual(contentsOfFile1, contentsOfFile2, { strict: true, ignoreKeys: ignoreKeys });
    return result;
}

function compare(folder1, folder2) {
    var options = {
        compareSize: false,
        compareContent: true,
        compareFileSync: fileComparator,
        excludeFilter: "cursor.json"
    };

    var result = dirCompare.compareSync(folder1, folder2, options);

    console.log('equal: ' + result.equal);
    console.log('distinct: ' + result.distinct);
    console.log('left: ' + result.left);
    console.log('right: ' + result.right);
    console.log('differences: ' + result.differences);
    console.log('same: ' + result.same);
    var format = require('util').format;
    result.diffSet.forEach(function (entry) {
        if (entry.state == "equal") return;

        var state = {
            'equal' : '==',
            'left' : '->',
            'right' : '<-',
            'distinct' : '<>'
        }[entry.state];

        var name1 = entry.name1 ? entry.name1 : '';
        var name2 = entry.name2 ? entry.name2 : '';
        console.log(format('%s(%s)%s%s(%s)', name1, entry.type1, state, name2, entry.type2));
    });
}

run();