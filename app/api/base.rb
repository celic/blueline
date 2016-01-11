module API
    class Base < Grape::API

        prefix :api
        format :json

		mount Players
		mount Games
		mount Teams

		add_swagger_documentation hide_format: true
    end
end
