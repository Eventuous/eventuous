import * as pulumi from "@pulumi/pulumi";
import * as gcp from "@pulumi/gcp";
import * as docker from "@pulumi/docker";
import {input as inputs} from "@pulumi/gcp/types";

const config = new pulumi.Config();
const connectorName = config.require("vpcConnectorName");

const imageName = "bookings";
const location = gcp.config.region || "europe-west2";

const myImage = new docker.Image(imageName, {
    imageName: pulumi.interpolate`gcr.io/${gcp.config.project}/${imageName}:latest`,
    build: {
        context: "../..",
        extraOptions: ["--platform=linux/amd64"],
        dockerfile: "../../Bookings/Dockerfile",
    },
});

let env: pulumi.Input<inputs.cloudrun.ServiceTemplateSpecContainerEnv>[] = [
    {name: "Mongo__ConnectionString", value: config.require("mongoConnectionString"),},
    {name: "Mongo__User", value: config.require("mongoUser"),},
    {name: "Mongo__Password", value: config.require("mongoPassword"),},
    {name: "EventStore__ConnectionString", value: config.require("esdbConnectionString"),},
    {name: "ASPNETCORE_ENVIRONMENT", value: "Development",},
    {name: "ASPNETCORE_URLS", value: "http://*:5003",},
];

const otelEndpoint = config.get("otelEndpoint");
if (otelEndpoint) {
    env.push({name: "OTEL_EXPORTER_OTLP_ENDPOINT", value: otelEndpoint,});
}
const otelHeaders = config.get("otelHeaders");
if (otelHeaders) {
    env.push({name: "OTEL_EXPORTER_OTLP_HEADERS", value: otelHeaders,});
}

const bookingsService = new gcp.cloudrun.Service("bookings", {
    location,
    template: {
        spec: {
            containers: [{
                image: myImage.imageName,
                resources: {
                    limits: {
                        memory: "512M",
                        cpu: "1000m"
                    },
                },
                ports: [{
                    name: "http1",
                    containerPort: 5003,
                }],
                envs: env,
            }],
            containerConcurrency: 50,
        },
        metadata: {
            annotations: {
                "autoscaling.knative.dev/maxScale": "1",
                "run.googleapis.com/vpc-access-connector": connectorName,
                "run.googleapis.com/vpc-access-egress": "private-ranges-only"
            }
        }
    },
    autogenerateRevisionName: true,
});

const iam = new gcp.cloudrun.IamMember("iam-everyone", {
    service: bookingsService.name,
    location,
    role: "roles/run.invoker",
    member: "allUsers",
});

// Export the URL
export const url = bookingsService.statuses[0].url;
