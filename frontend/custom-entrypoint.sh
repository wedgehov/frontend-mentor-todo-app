#!/bin/sh
set -e

# Substitute environment variables in the Nginx config template.
envsubst '$API_URL' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf

# Start Nginx in the foreground.
exec nginx -g 'daemon off;'