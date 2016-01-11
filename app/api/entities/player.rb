module API
	module Entities
		class Player < Grape::Entity

			expose :id, :name, :position, :key

			expose :team, using: API::Entities::Team, if: :team 

		end
	end
end
