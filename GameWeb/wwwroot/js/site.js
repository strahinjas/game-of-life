var app = angular.module('GameOfLife', ['ui.bootstrap']);
app.run(function () { });

app.controller('GameOfLifeController', ['$rootScope', '$scope', '$http', '$timeout', function ($rootScope, $scope, $http, $timeout) {
    $scope.simulationStarted = false;

    $scope.refresh = function () {
        $scope.getGrid();
        $scope.getTime();
        $scope.getStats();
    };

    $scope.generate = function (n) {
        $http.post('api/game/init?n=' + n)
            .then(function (data, status) {
                $scope.gridGenerated = true;
                $scope.refresh();
            });
    };

    $scope.start = function () {
        $http.post('api/game/start')
            .then(function (data, status) {
                $scope.simulationStarted = true;
                $scope.interval = setInterval($scope.refresh, 200);
            });;
    };

    $scope.stop = function () {
        $http.post('api/game/stop')
            .then(function (data, status) {
                $scope.simulationStarted = false;
                clearInterval($scope.interval);
            });;
    };

    $scope.style = function (cell) {
        if (cell) {
            return { 'background-color': 'darkslategrey' };
        } else {
            return { 'background-color': 'white' };
        }
    };

    $scope.getGrid = function () {
        $http.get('api/game/grid')
            .then(function (response) {
                $scope.grid = response.data;
            }, function (response) {
                $scope.grid = undefined;
            });
    };

    $scope.getTime = function () {
        $http.get('api/game/time')
            .then(function (response) {
                $scope.time = response.data;
            }, function (response) {
                $scope.time = undefined;
            });
    };

    $scope.getStats = function () {
        $http.get('api/game/stats')
            .then(function (response) {
                $scope.stats = response.data;
            }, function (response) {
                $scope.stats = undefined;
            });
    };
}]);