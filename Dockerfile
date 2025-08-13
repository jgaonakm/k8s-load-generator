FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
ARG TARGETPLATFORM
ARG BUILDPLATFORM
RUN echo "I am running on $BUILDPLATFORM, building for $TARGETPLATFORM"
ENV DOTNET_EnableWriteXorExecute=0
WORKDIR /App

# Copy everything
COPY . ./

# Restore as distinct layer
RUN dotnet restore -v diag

# Build and publish a release
RUN dotnet publish -c Release -o out -v diag

# Build runtime image, using multi-stage builds
# https://docs.docker.com/build/building/multi-stage/

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /App
COPY --from=build-env /App/out .

ENTRYPOINT [ "dotnet", "load-gen.dll" ]