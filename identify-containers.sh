#!/bin/bash
echo "Please copy the output below and send it to me:"
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""
echo "Also, list of docker networks:"
docker network ls
