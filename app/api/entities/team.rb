module API
	module Entities
		class Team < Grape::Entity

			expose :id, :abbreviation, :full_name, :name, :city, :state, :wins, :division_id, :wins, :losses, :ot, :so, :points

			expose :players, using: API::Entities::Player, if: :roster

			expose :games, if: :recent do |team, opts|

				# done this way to prevent option inheritance
				API::Entities::Game.represent team.games.order(date: :desc).limit(5), Hash.new
			end
		end
	end
end
