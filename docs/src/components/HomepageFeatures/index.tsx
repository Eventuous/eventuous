import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';

type FeatureItem = {
    title: string;
    // Svg: React.ComponentType<React.ComponentProps<'svg'>>;
    description: JSX.Element;
};

const FeatureList: FeatureItem[] = [
    {
        title: 'Domain and persistence',
        // Svg: require('@site/static/img/undraw_docusaurus_mountain.svg').default,
        description: (
            <>
                Event-sourced Aggregate base classes and Aggregate Store building blocks to build your domain model,
                and persist state transitions as events using EventStoreDB, PostgreSQL and Microsoft SQL Server.
            </>
        ),
    },
    {
        title: 'Subscriptions',
        // Svg: require('@site/static/img/undraw_docusaurus_tree.svg').default,
        description: (
            <>
                Real-time subscriptions to support event reactors and read model projections,
                using EventStoreDB, PostgreSQL, Microsoft SQL Server, Google PubSub, and RabbitMQ.
            </>
        ),
    },
    {
        title: 'Messaging',
        // Svg: require('@site/static/img/undraw_docusaurus_react.svg').default,
        description: (
            <>
                Produce and consume messages using EventStoreDB, PostgreSQL, Microsoft SQL Server, Google PubSub,
                and RabbitMQ.
            </>
        ),
    },
];

function Feature({title, description}: FeatureItem) {
    return (
        <div className={clsx('col col--4')}>
            <div className="text--center">
                {/*<i className="fas fa-sitemap"/>*/}
                {/*<Svg className={styles.featureSvg} role="img" />*/}
            </div>
            <div className="text--center padding-horiz--md">
                <h3>{title}</h3>
                <p>{description}</p>
            </div>
        </div>
    );
}

export default function HomepageFeatures(): JSX.Element {
    return (
        <section className={styles.features}>
            <div className="container">
                <div className="row">
                    {FeatureList.map((props, idx) => (
                        <Feature key={idx} {...props} />
                    ))}
                </div>
            </div>
        </section>
    );
}
