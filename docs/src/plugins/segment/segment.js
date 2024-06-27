import ExecutionEnvironment from "@docusaurus/ExecutionEnvironment";

export default (function () {
    if (!ExecutionEnvironment.canUseDOM) {
        return null;
    }

    return {
        onRouteUpdate() {
            // if (!window.analytics) return;

            // setTimeout(() => window.analytics.page(), 0);
        },
    };
})();