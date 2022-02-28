# One-click publishing

On TeamCity, each product is represented as a project. Products, that should be deployed at once, are grouped into a parent project.

There is a master project with a "Publish All" build configuration, service as the publishing button for the whole group.

This configuration depends on all "Publish [Public]" build configurations of all projects in the group.

(It could depend on some projects only, but that would be more difficult to maintain and it wouldn't bring any benefits.)

As such, pushing the button first triggers the publishing of the projects. If all of them succeed, we programmatically check that all of the projects in the same group have been actually published. This is to avoid having projects which were added to the group, but not to the list of dependencies to this "Publish All" build configuration.

(Checking the dependencies upfront leads to implementing features which are already implemented in TC.)