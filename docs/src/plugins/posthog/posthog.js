import ExecutionEnvironment from '@docusaurus/ExecutionEnvironment';

export default (function () {
  if (!ExecutionEnvironment.canUseDOM) {
    return null;
  }

  return {
    onRouteUpdate() {
      window.posthog.capture('$pageview');
    },
  };
})();