module API
	module Entities
		class Game < Grape::Entity

			expose :home, using: API::Entities::Team
			expose :home_stats

			expose :away, using: API::Entities::Team
			expose :away_stats

			expose :date

		end
	end
end
