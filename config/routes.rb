AmqpExample::Application.routes.draw do

	mount API::Base => '/'

	mount GrapeSwaggerRails::Engine => '/docs'
end
