import React from 'react';
import Metadata from '@theme-original/DocItem/Metadata';
import {useDoc} from "@docusaurus/theme-common/internal";
import {useSidebarBreadcrumbs} from "@docusaurus/theme-common/internal";
import ExecutionEnvironment from "@docusaurus/ExecutionEnvironment";
import {useLocation} from "@docusaurus/router";

let previous = null;

const robots = ["Googlebot"];

export default function MetadataWrapper(props) {
    if (ExecutionEnvironment.canUseDOM) {
        const x = useDoc();
        if (previous !== x && robots.find(r => window.navigator.userAgent.includes(r)) === undefined) {
            previous = x;
            const sb = useSidebarBreadcrumbs();
            const category = sb[0];
            const page = sb[sb.length - 1];
            const location = useLocation();
            const area = page.docId === undefined
                ? page.href.includes('/connector/') ? 'Connector' : 'Core'
                : page.docId.startsWith('connector') ? 'Connector' : 'Core';
            if (window.analytics) {
                setTimeout(() => window.analytics.page(page.label, {
                    category: category.label,
                    path: location.pathname,
                    area: area
                }), 0);
            } else {
                console.log('location', location);
            }
        }
    }
    return (
        <>
            <Metadata {...props} />
        </>
    );
}
